using MySqlConnector;
using Scanner.Server.DAL;
using Scanner.Web.Models;
using System.Security.Cryptography;

namespace Scanner.Web.Services;

public sealed class MySqlAccountService(IMySqlConnectionFactory connectionFactory) : IAccountService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DefaultIterations = 100_000;
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(8);

    public async Task<IReadOnlyList<AccountDomain>> GetDomainsAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `domain_name` FROM `user_domains` WHERE `is_enabled` = 1 ORDER BY `id`;";
        List<AccountDomain> domains = new();
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) domains.Add(new AccountDomain(reader.GetInt32("id"), reader.GetString("domain_name")));
        return domains;
    }

    public async Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        string username = RequiredUsername(request.Username);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = HashPassword(request.Password, salt, DefaultIterations);
        DateTime now = DateTime.UtcNow;

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            AccountDomain domain = await GetDomainAsync(connection, transaction, request.DomainId, cancellationToken)
                ?? throw new ArgumentException("选择的域不存在或已停用。");
            await using MySqlCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO `app_users`
                    (`uuid`, `username`, `domain_id`, `password_salt`, `password_hash`, `password_algorithm`, `password_iterations`, `role`, `status`, `password_changed_at`)
                VALUES
                    (@uuid, @username, @domainId, @salt, @hash, 'PBKDF2-SHA256', @iterations, 'user', 'active', @now);
                """;
            insert.Parameters.Add("@uuid", MySqlDbType.VarChar, 36).Value = Guid.NewGuid().ToString();
            insert.Parameters.Add("@username", MySqlDbType.VarChar, 100).Value = username;
            insert.Parameters.Add("@domainId", MySqlDbType.UInt32).Value = request.DomainId;
            insert.Parameters.Add("@salt", MySqlDbType.VarBinary, SaltSize).Value = salt;
            insert.Parameters.Add("@hash", MySqlDbType.VarBinary, HashSize).Value = hash;
            insert.Parameters.Add("@iterations", MySqlDbType.UInt32).Value = DefaultIterations;
            insert.Parameters.Add("@now", MySqlDbType.DateTime).Value = now;
            try { await insert.ExecuteNonQueryAsync(cancellationToken); }
            catch (MySqlException exception) when (exception.Number == 1062) { throw new AccountAlreadyExistsException(username); }

            ulong userId = checked((ulong)insert.LastInsertedId);
            AccountResult result = await CreateSessionAsync(connection, transaction, userId, username, null, domain, "user", now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<AccountResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        string username = RequiredUsername(request.Username);
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using MySqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT u.`id`, u.`username`, u.`display_name`, u.`password_salt`, u.`password_hash`, u.`password_iterations`,
                       u.`role`, u.`status`, u.`failed_login_count`, u.`locked_until`, d.`id` AS `domain_id`, d.`domain_name`, d.`is_enabled`
                FROM `app_users` u
                INNER JOIN `user_domains` d ON d.`id` = u.`domain_id`
                WHERE u.`username` = @username LIMIT 1 FOR UPDATE;
                """;
            command.Parameters.Add("@username", MySqlDbType.VarChar, 100).Value = username;
            UserRow? user = null;
            await using (MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (await reader.ReadAsync(cancellationToken)) user = ReadUser(reader);
            }
            if (user is null) return null;
            DateTime now = DateTime.UtcNow;
            if (!user.DomainEnabled || !string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase)
                || user.LockedUntil.HasValue && user.LockedUntil.Value > now) return null;

            byte[] suppliedHash = HashPassword(request.Password, user.PasswordSalt, user.Iterations);
            if (!CryptographicOperations.FixedTimeEquals(suppliedHash, user.PasswordHash))
            {
                int failed = user.FailedLoginCount + 1;
                await using MySqlCommand failure = connection.CreateCommand();
                failure.Transaction = transaction;
                failure.CommandText = "UPDATE `app_users` SET `failed_login_count`=@failed, `locked_until`=@lockedUntil WHERE `id`=@id;";
                failure.Parameters.AddWithValue("@failed", failed);
                failure.Parameters.Add("@lockedUntil", MySqlDbType.DateTime).Value = failed >= MaxFailedLogins ? now.Add(LockDuration) : DBNull.Value;
                failure.Parameters.AddWithValue("@id", user.Id);
                await failure.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            await using (MySqlCommand success = connection.CreateCommand())
            {
                success.Transaction = transaction;
                success.CommandText = "UPDATE `app_users` SET `failed_login_count`=0, `locked_until`=NULL, `last_login_at`=@now WHERE `id`=@id;";
                success.Parameters.Add("@now", MySqlDbType.DateTime).Value = now;
                success.Parameters.AddWithValue("@id", user.Id);
                await success.ExecuteNonQueryAsync(cancellationToken);
            }
            AccountResult result = await CreateSessionAsync(connection, transaction, user.Id, user.Username, user.DisplayName,
                new AccountDomain(user.DomainId, user.DomainName), user.Role, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<AccountResult?> GetCurrentAsync(string token, CancellationToken cancellationToken = default)
    {
        byte[] tokenHash = TokenHash(token);
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.`username`, u.`display_name`, u.`role`, d.`id` AS `domain_id`, d.`domain_name`, s.`expires_at`
            FROM `user_sessions` s
            INNER JOIN `app_users` u ON u.`id` = s.`user_id`
            INNER JOIN `user_domains` d ON d.`id` = u.`domain_id`
            WHERE s.`token_hash`=@tokenHash AND s.`revoked_at` IS NULL AND s.`expires_at`>@now
              AND u.`status`='active' AND d.`is_enabled`=1 LIMIT 1;
            """;
        command.Parameters.Add("@tokenHash", MySqlDbType.Binary, 32).Value = tokenHash;
        command.Parameters.Add("@now", MySqlDbType.DateTime).Value = DateTime.UtcNow;
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new AccountResult(reader.GetString("username"), reader.IsDBNull(reader.GetOrdinal("display_name")) ? null : reader.GetString("display_name"),
            new AccountDomain(reader.GetInt32("domain_id"), reader.GetString("domain_name")), reader.GetString("role"), token,
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime("expires_at"), DateTimeKind.Utc)));
    }

    public async Task LogoutAsync(string token, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE `user_sessions` SET `revoked_at`=@now WHERE `token_hash`=@tokenHash AND `revoked_at` IS NULL;";
        command.Parameters.Add("@now", MySqlDbType.DateTime).Value = DateTime.UtcNow;
        command.Parameters.Add("@tokenHash", MySqlDbType.Binary, 32).Value = TokenHash(token);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<AccountDomain?> GetDomainAsync(MySqlConnection connection, MySqlTransaction transaction, int id, CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT `id`, `domain_name` FROM `user_domains` WHERE `id`=@id AND `is_enabled`=1 LIMIT 1;";
        command.Parameters.AddWithValue("@id", id);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new AccountDomain(reader.GetInt32("id"), reader.GetString("domain_name")) : null;
    }

    private static async Task<AccountResult> CreateSessionAsync(MySqlConnection connection, MySqlTransaction transaction, ulong userId,
        string username, string? displayName, AccountDomain domain, string role, DateTime now, CancellationToken cancellationToken)
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        DateTime expiresAt = now.Add(TokenLifetime);
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO `user_sessions` (`user_id`,`token_hash`,`expires_at`,`created_at`,`last_used_at`) VALUES (@userId,@tokenHash,@expiresAt,@now,@now);";
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.Add("@tokenHash", MySqlDbType.Binary, 32).Value = TokenHash(token);
        command.Parameters.Add("@expiresAt", MySqlDbType.DateTime).Value = expiresAt;
        command.Parameters.Add("@now", MySqlDbType.DateTime).Value = now;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new AccountResult(username, displayName, domain, role, token, new DateTimeOffset(expiresAt, TimeSpan.Zero));
    }

    private static string RequiredUsername(string? value)
    {
        string username = value?.Trim() ?? string.Empty;
        if (username.Length is < 3 or > 100) throw new ArgumentException("用户名长度必须在 3 到 100 个字符之间。");
        return username;
    }

    private static byte[] HashPassword(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);
    private static byte[] TokenHash(string token) => SHA256.HashData(Convert.FromBase64String(token));

    private static UserRow ReadUser(MySqlDataReader reader) => new(
        reader.GetUInt64("id"), reader.GetString("username"), reader.IsDBNull(reader.GetOrdinal("display_name")) ? null : reader.GetString("display_name"),
        (byte[])reader["password_salt"], (byte[])reader["password_hash"], reader.GetInt32("password_iterations"), reader.GetString("role"),
        reader.GetString("status"), reader.GetInt32("failed_login_count"), reader.IsDBNull(reader.GetOrdinal("locked_until")) ? null : reader.GetDateTime("locked_until"),
        reader.GetInt32("domain_id"), reader.GetString("domain_name"), reader.GetBoolean("is_enabled"));

    private sealed record UserRow(ulong Id, string Username, string? DisplayName, byte[] PasswordSalt, byte[] PasswordHash, int Iterations,
        string Role, string Status, int FailedLoginCount, DateTime? LockedUntil, int DomainId, string DomainName, bool DomainEnabled);
}
