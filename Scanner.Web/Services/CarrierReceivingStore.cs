using Microsoft.AspNetCore.DataProtection;
using MySqlConnector;
using Scanner.Server.DAL;

namespace Scanner.Web.Services;

public sealed record CarrierSettingsEdit(bool Enabled, bool Sandbox, string ClientId, string? Secret, bool ClearSecret, int Revision);
public sealed record CarrierSettingsView(string Carrier, bool Enabled, bool Sandbox, string ClientId, bool HasSecret, int Revision);
public sealed record ReceivingItem(ulong Id, string Carrier, string TrackingNumber, string Reference,
    DateTime CreatedAt, DateTime? ReceivedAt, string? ReceivedBy, string CarrierStatus, bool Delivered, DateTime? CheckedAt,
    DateTime? LastAttemptAt = null, string LastError = "")
{
    public string Reconciliation => ReceivedAt.HasValue ? "已实收" : Delivered ? "显示送达但未实收" : "待收货";
}

public sealed class CarrierReceivingStore(IMySqlConnectionFactory connections, IDataProtectionProvider protection)
{
    private IDataProtector Protector(int domain, string carrier) => protection.CreateProtector("Scanner.CarrierCredentials.v1", domain.ToString(), carrier);

    private async Task<T> Execute<T>(Func<MySqlConnection, Task<T>> action, CancellationToken ct)
    {
        try
        {
            await using var connection = await connections.OpenConnectionAsync(ct);
            return await action(connection);
        }
        catch (MySqlException ex) when (ex.Number == 1146)
        { throw new ApiException(503, "请先执行 Database/022_carrier_receiving.sql 和 023_bluetti_receiving_sync.sql。"); }
        catch (MySqlException ex) when (ex.Number == 1054)
        { throw new ApiException(503, "数据库字段未更新，请执行 Database/023_bluetti_receiving_sync.sql。"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("MySQL access is disabled", StringComparison.Ordinal))
        { throw new ApiException(503, "服务器数据库未启用，请先完成现有 Web 数据库连接配置。"); }
    }

    public Task<CarrierSettingsView> Settings(int domain, string carrier, CancellationToken ct) => Execute(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT enabled,sandbox,client_id,secret_protected,revision FROM carrier_settings WHERE domain_id=@domain AND carrier=@carrier";
        command.Parameters.AddWithValue("@domain", domain); command.Parameters.AddWithValue("@carrier", carrier);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(carrier, reader.GetBoolean(0), reader.GetBoolean(1), reader.GetString(2), reader.GetString(3).Length > 0, reader.GetInt32(4))
            : new CarrierSettingsView(carrier, false, true, "", false, 0);
    }, ct);

    public Task<bool> SaveSettings(int domain, string carrier, string user, CarrierSettingsEdit edit, CancellationToken ct) => Execute(async connection =>
    {
        string clientId = edit.ClientId?.Trim() ?? "";
        if (clientId.Length > 512 || (edit.Secret?.Length ?? 0) > 4096 || edit.Revision < 0 || edit.ClearSecret && !string.IsNullOrEmpty(edit.Secret))
            throw new ArgumentException("凭据长度或配置版本无效，清除密钥时不能同时填写新密钥。");
        await using var tx = await connection.BeginTransactionAsync(ct);
        using var command = connection.CreateCommand(); command.Transaction = tx;
        command.Parameters.AddWithValue("@domain", domain); command.Parameters.AddWithValue("@carrier", carrier);
        command.CommandText = "SELECT secret_protected,revision,client_id FROM carrier_settings WHERE domain_id=@domain AND carrier=@carrier FOR UPDATE";
        string secret = "", previousId = ""; int revision = 0;
        await using (var reader = await command.ExecuteReaderAsync(ct))
            if (await reader.ReadAsync(ct)) { secret = reader.GetString(0); revision = reader.GetInt32(1); previousId = reader.GetString(2); }
        if (revision != edit.Revision) throw new ApiException(409, "配置已被其他管理员修改，请重新加载后保存。");
        if (previousId != clientId && string.IsNullOrEmpty(edit.Secret) && !edit.ClearSecret && secret.Length > 0)
            throw new ArgumentException("更换 Client ID 时请同时填写新密钥或清除原密钥。");
        if (edit.ClearSecret) secret = "";
        else if (!string.IsNullOrEmpty(edit.Secret)) secret = Protector(domain, carrier).Protect(edit.Secret);
        if (edit.Enabled && (clientId.Length == 0 || secret.Length == 0)) throw new ArgumentException("启用前必须填写 Client ID 和密钥。");
        command.CommandText = revision == 0 ? """
            INSERT INTO carrier_settings(domain_id,carrier,enabled,sandbox,client_id,secret_protected,revision,updated_by,updated_at)
            VALUES(@domain,@carrier,@enabled,@sandbox,@client,@secret,1,@user,UTC_TIMESTAMP(3))
            """ : """
            UPDATE carrier_settings SET enabled=@enabled,sandbox=@sandbox,client_id=@client,secret_protected=@secret,
            revision=revision+1,updated_by=@user,updated_at=UTC_TIMESTAMP(3) WHERE domain_id=@domain AND carrier=@carrier AND revision=@revision
            """;
        command.Parameters.AddWithValue("@enabled", edit.Enabled); command.Parameters.AddWithValue("@sandbox", edit.Sandbox);
        command.Parameters.AddWithValue("@client", clientId); command.Parameters.AddWithValue("@secret", secret); command.Parameters.AddWithValue("@user", user);
        command.Parameters.AddWithValue("@revision", revision);
        try
        {
            if (await command.ExecuteNonQueryAsync(ct) == 0) throw new ApiException(409, "配置已被修改，请重新加载。");
        }
        catch (MySqlException ex) when (ex.Number is 1062 or 1213) { throw new ApiException(409, "配置正在被其他管理员修改，请重新加载后保存。"); }
        await tx.CommitAsync(ct); return true;
    }, ct);

    public Task<CarrierCredentials> Credentials(int domain, string carrier, CancellationToken ct) => Execute<CarrierCredentials>(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT enabled,sandbox,client_id,secret_protected FROM carrier_settings WHERE domain_id=@domain AND carrier=@carrier";
        command.Parameters.AddWithValue("@domain", domain); command.Parameters.AddWithValue("@carrier", carrier);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new ApiException(409, "请先配置承运商。");
        string secret = reader.GetString(3);
        try { return new(reader.GetBoolean(0), reader.GetBoolean(1), reader.GetString(2), secret.Length == 0 ? "" : Protector(domain, carrier).Unprotect(secret)); }
        catch (System.Security.Cryptography.CryptographicException) { throw new ApiException(503, "无法解密承运商密钥，请恢复 Data Protection 密钥或在管理页面重新填写凭据。"); }
    }, ct);

    public Task<bool> Register(int domain, string carrier, string number, string reference, string user, bool receive, CancellationToken ct) => Execute(async connection =>
    {
        if (reference.Length > 200 || reference.Any(char.IsControl)) throw new ArgumentException("RMA/备注最多 200 字符。");
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO carrier_receiving(domain_id,carrier,tracking_number,reference,created_at,created_by,received_at,received_by)
            VALUES(@domain,@carrier,@number,@reference,UTC_TIMESTAMP(3),@user,IF(@receive,UTC_TIMESTAMP(3),NULL),IF(@receive,@user,NULL))
            ON DUPLICATE KEY UPDATE received_by=IF(@receive AND received_at IS NULL,@user,received_by),received_at=IF(@receive AND received_at IS NULL,UTC_TIMESTAMP(3),received_at)
            """;
        command.Parameters.AddWithValue("@domain", domain); command.Parameters.AddWithValue("@carrier", carrier); command.Parameters.AddWithValue("@number", number);
        command.Parameters.AddWithValue("@reference", reference); command.Parameters.AddWithValue("@user", user); command.Parameters.AddWithValue("@receive", receive);
        await command.ExecuteNonQueryAsync(ct); return true;
    }, ct);

    public Task<List<ReceivingItem>> List(int domain, ulong before, string search, CancellationToken ct) => Execute(async connection =>
    {
        if (search.Length > 200) throw new ArgumentException("搜索内容过长。");
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,carrier,tracking_number,reference,created_at,received_at,received_by,carrier_status,delivered,checked_at,last_attempt_at,last_error
            FROM carrier_receiving WHERE domain_id=@domain AND (@before=0 OR id<@before)
            AND (@search='' OR LOCATE(@search,tracking_number)>0 OR LOCATE(@search,reference)>0) ORDER BY id DESC LIMIT 100
            """;
        command.Parameters.AddWithValue("@domain", domain); command.Parameters.AddWithValue("@before", before); command.Parameters.AddWithValue("@search", search);
        await using var reader = await command.ExecuteReaderAsync(ct); var rows = new List<ReceivingItem>();
        DateTime Utc(int i) => DateTime.SpecifyKind(reader.GetDateTime(i), DateTimeKind.Utc);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetUInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), Utc(4),
            reader.IsDBNull(5) ? null : Utc(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetString(7), reader.GetBoolean(8), reader.IsDBNull(9) ? null : Utc(9),
            reader.IsDBNull(10) ? null : Utc(10), reader.GetString(11)));
        return rows;
    }, ct);

    public Task<bool> UpdateTracking(int domain, string carrier, string number, CarrierTrackingResult result, CancellationToken ct) => Execute(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE carrier_receiving SET carrier_status=@status,delivered=@delivered,checked_at=UTC_TIMESTAMP(3),last_attempt_at=UTC_TIMESTAMP(3),last_error='' WHERE domain_id=@domain AND carrier=@carrier AND tracking_number=@number";
        command.Parameters.AddWithValue("@domain", domain); command.Parameters.AddWithValue("@carrier", carrier); command.Parameters.AddWithValue("@number", number);
        command.Parameters.AddWithValue("@status", result.Status); command.Parameters.AddWithValue("@delivered", result.Delivered);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }, ct);
}
