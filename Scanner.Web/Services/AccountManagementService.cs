using MySqlConnector;
using Scanner.Server.DAL;
using Scanner.Web.Models;
using System.Security.Cryptography;

namespace Scanner.Web.Services;

public sealed class AccountManagementService(IMySqlConnectionFactory connections, ILogger<AccountManagementService> logger) : IAccountManagementService
{
    private const int Iterations = 100_000;
    public static void RequireAdmin(AccountResult actor)
    {
        if (actor.Role != "admin") throw new ApiException(403, "仅管理员可以管理用户。");
    }
    public static void ValidatePassword(string? password, string? confirm)
    {
        if (password is null || password.Length is < 6 or > 4096 || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("新密码需要 6–4096 个字符，不能全为空白。");
        if (password != confirm) throw new ArgumentException("两次密码不一致。");
    }
    public static void ValidateCreate(CreateAccountRequest request)
    {
        string username = request.Username?.Trim() ?? "";
        if (username.Length is < 1 or > 100 || username.Any(char.IsControl) || (request.DisplayName?.Length ?? 0) > 100
            || request.DisplayName?.Any(char.IsControl) == true || !AccountSecurity.IsKnownRole(request.Role))
            throw new ArgumentException("用户名、显示名称或角色无效。");
        ValidatePassword(request.Password, request.ConfirmPassword);
    }
    public async Task<IReadOnlyList<ManagedAccount>> List(AccountResult actor, ulong after, string search, CancellationToken ct)
    {
        RequireAdmin(actor);
        if (search.Length > 100) throw new ArgumentException("搜索内容最多 100 个字符。");
        await using var c = await connections.OpenConnectionAsync(ct); using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT id,username,display_name,role,status FROM app_users
            WHERE domain_id=@domain AND id>@after AND (@search='' OR LOCATE(@search,username)>0 OR LOCATE(@search,COALESCE(display_name,''))>0)
            ORDER BY id LIMIT 100
            """;
        cmd.Parameters.AddWithValue("@domain", actor.Domain.Id); cmd.Parameters.AddWithValue("@after", after); cmd.Parameters.AddWithValue("@search", search);
        await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<ManagedAccount>();
        while (await r.ReadAsync(ct)) result.Add(new(r.GetUInt64(0).ToString(), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3), r.GetString(4)));
        return result;
    }
    public async Task<ManagedAccount> Create(AccountResult actor, CreateAccountRequest request, CancellationToken ct)
    {
        RequireAdmin(actor); ValidateCreate(request);
        await using var c = await connections.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        await LockUsers(c, tx, actor.UserId, actor.UserId, ct);
        await Revalidate(c, tx, actor, true, ct);
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(request.Password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO app_users(uuid,username,display_name,domain_id,password_salt,password_hash,password_algorithm,password_iterations,role,status,password_changed_at)
            VALUES(@uuid,@username,@display,@domain,@salt,@hash,'PBKDF2-SHA256',@iterations,@role,'active',UTC_TIMESTAMP())
            """;
        cmd.Parameters.AddWithValue("@uuid", Guid.NewGuid().ToString("D")); cmd.Parameters.AddWithValue("@username", request.Username.Trim());
        cmd.Parameters.AddWithValue("@display", string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim());
        cmd.Parameters.AddWithValue("@domain", actor.Domain.Id); cmd.Parameters.AddWithValue("@salt", salt); cmd.Parameters.AddWithValue("@hash", hash);
        cmd.Parameters.AddWithValue("@iterations", Iterations); cmd.Parameters.AddWithValue("@role", request.Role);
        try { await cmd.ExecuteNonQueryAsync(ct); }
        catch (MySqlException ex) when (ex.Number == 1062) { throw new ApiException(409, "用户名已被使用，请换一个用户名。"); }
        var result = new ManagedAccount(cmd.LastInsertedId.ToString(), request.Username.Trim(), string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(), request.Role, "active");
        await tx.CommitAsync(ct);
        logger.LogInformation("Account {Target} created by user {Actor} in domain {Domain}", result.Id, actor.UserId, actor.Domain.Id);
        // No session is created for the new user or returned to the administrator.
        return result;
    }
    public async Task ChangePassword(AccountResult actor, ChangeAccountPasswordRequest request, CancellationToken ct)
    {
        ValidatePassword(request.NewPassword, request.ConfirmPassword);
        if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 4096) throw new ArgumentException("请填写当前密码。");
        await using var c = await connections.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        var users = await LockUsers(c, tx, actor.UserId, actor.UserId, ct);
        await Revalidate(c, tx, actor, false, ct);
        var user = users.SingleOrDefault(u => u.Id == actor.UserId) ?? throw new ApiException(401, "账号已失效，请重新登录。");
        if (!AccountSecurity.VerifyPassword(request.CurrentPassword, user.Salt, user.Hash, user.Algorithm, user.Iterations))
            throw new ApiException(400, "当前密码不正确。");
        if (request.CurrentPassword == request.NewPassword) throw new ArgumentException("新密码不能与当前密码相同。");
        await WritePassword(c, tx, actor.UserId, request.NewPassword, ct); await tx.CommitAsync(ct);
        logger.LogInformation("User {Actor} changed own password in domain {Domain}", actor.UserId, actor.Domain.Id);
    }
    public async Task ResetPassword(AccountResult actor, ulong userId, ResetAccountPasswordRequest request, CancellationToken ct)
    {
        RequireAdmin(actor); ValidatePassword(request.NewPassword, request.ConfirmPassword);
        if (userId == 0) throw new ArgumentException("请选择用户。");
        if (userId == actor.UserId) throw new ArgumentException("修改自己的密码请使用“修改我的密码”，并验证当前密码。");
        await using var c = await connections.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        var users = await LockUsers(c, tx, actor.UserId, userId, ct);
        await Revalidate(c, tx, actor, true, ct);
        if (!users.Any(u => u.Id == userId && u.DomainId == actor.Domain.Id)) throw new ApiException(404, "当前域中没有该用户。");
        await WritePassword(c, tx, userId, request.NewPassword, ct); await tx.CommitAsync(ct);
        logger.LogInformation("User {Target} password reset by {Actor} in domain {Domain}", userId, actor.UserId, actor.Domain.Id);
    }
    private sealed record LockedUser(ulong Id, int DomainId, byte[] Salt, byte[] Hash, string Algorithm, uint Iterations);
    private static async Task<List<LockedUser>> LockUsers(MySqlConnection c, MySqlTransaction tx, ulong actor, ulong target, CancellationToken ct)
    {
        using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "SELECT id,domain_id,password_salt,password_hash,password_algorithm,password_iterations FROM app_users WHERE id IN (@actor,@target) ORDER BY id FOR UPDATE";
        cmd.Parameters.AddWithValue("@actor", actor); cmd.Parameters.AddWithValue("@target", target);
        await using var r = await cmd.ExecuteReaderAsync(ct); var users = new List<LockedUser>();
        while (await r.ReadAsync(ct)) users.Add(new(r.GetUInt64(0), r.GetInt32(1), (byte[])r[2], (byte[])r[3], r.GetString(4), r.GetUInt32(5)));
        return users;
    }
    private static async Task Revalidate(MySqlConnection c, MySqlTransaction tx, AccountResult actor, bool admin, CancellationToken ct)
    {
        byte[] token;
        try { token = SHA256.HashData(Convert.FromBase64String(actor.Token)); }
        catch (FormatException) { throw new ApiException(401, "登录已失效，请重新登录。"); }
        using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT u.id FROM app_users u JOIN user_domains d ON d.id=u.domain_id JOIN user_sessions s ON s.user_id=u.id
            WHERE u.id=@actor AND u.domain_id=@domain AND u.status='active' AND d.is_enabled=1
            AND (u.locked_until IS NULL OR u.locked_until<=UTC_TIMESTAMP())
            AND u.role IN ('admin','user','viewer') AND (@admin=0 OR u.role='admin')
            AND s.token_hash=@token AND s.revoked_at IS NULL AND s.expires_at>UTC_TIMESTAMP()
            AND u.credential_version=s.credential_version
            AND (u.password_changed_at IS NULL OR u.password_changed_at<=s.created_at) LIMIT 1 FOR UPDATE
            """;
        cmd.Parameters.AddWithValue("@actor", actor.UserId); cmd.Parameters.AddWithValue("@domain", actor.Domain.Id);
        cmd.Parameters.AddWithValue("@admin", admin); cmd.Parameters.AddWithValue("@token", token);
        if (await cmd.ExecuteScalarAsync(ct) is null) throw new ApiException(401, "登录或管理权限已变更，请重新登录。");
    }
    private static async Task WritePassword(MySqlConnection c, MySqlTransaction tx, ulong userId, string password, CancellationToken ct)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE app_users SET password_salt=@salt,password_hash=@hash,password_algorithm='PBKDF2-SHA256',password_iterations=@iterations,
              password_changed_at=UTC_TIMESTAMP(),credential_version=credential_version+1 WHERE id=@user
            """;
        cmd.Parameters.AddWithValue("@salt", salt); cmd.Parameters.AddWithValue("@hash", hash); cmd.Parameters.AddWithValue("@iterations", Iterations); cmd.Parameters.AddWithValue("@user", userId);
        await cmd.ExecuteNonQueryAsync(ct);
        // Works both with and without the existing password-version trigger. Disabled/locked users remain disabled/locked.
        cmd.CommandText = "UPDATE user_sessions SET revoked_at=UTC_TIMESTAMP() WHERE user_id=@user AND revoked_at IS NULL";
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
