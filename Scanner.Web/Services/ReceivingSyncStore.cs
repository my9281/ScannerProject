using Microsoft.AspNetCore.DataProtection;
using MySqlConnector;
using Scanner.Server.DAL;

namespace Scanner.Web.Services;

public sealed record ReceivingSyncEdit(bool Enabled, string Username, string? Password, bool ClearPassword, string SiteId,
    int IntervalSeconds, bool AutoTrack, int TrackIntervalSeconds, int Revision);
public sealed record ReceivingSyncSettings(bool Enabled, string Username, bool HasPassword, string SiteId, int IntervalSeconds,
    bool AutoTrack, int TrackIntervalSeconds, int Revision, DateTime? NextRunAt, DateTime? LastAttemptAt, DateTime? LastSuccessAt,
    string LastError, int LastRowCount, int LastIssueCount);

public sealed class ReceivingSyncStore(IMySqlConnectionFactory connections, IDataProtectionProvider protection)
{
    internal const string BluettiDomainName = "Bluetti";

    public async Task<int> BluettiDomain(CancellationToken ct)
    {
        await using var c = await connections.OpenConnectionAsync(ct);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id FROM user_domains WHERE domain_name=@name AND is_enabled=1";
        cmd.Parameters.AddWithValue("@name", BluettiDomainName);
        var id = await cmd.ExecuteScalarAsync(ct) ?? throw new ApiException(409, "请先在域管理中创建 Bluetti 域。");
        return Convert.ToInt32(id);
    }

    private IDataProtector Protector(int domain) => protection.CreateProtector("Scanner.Receiving.Bluetti.Password.v1", domain.ToString());
    public static void Validate(ReceivingSyncEdit edit)
    {
        if (edit.Username.Length > 200 || edit.SiteId.Length > 100 || (edit.Password?.Length ?? 0) > 4096 || edit.Revision < 0
            || edit.IntervalSeconds is < 300 or > 86400 || edit.TrackIntervalSeconds is < 60 or > 86400
            || edit.ClearPassword && !string.IsNullOrEmpty(edit.Password)) throw new ArgumentException("配置无效：客户同步周期为 300–86400 秒，物流周期为 60–86400 秒。");
        if (edit.Enabled && (string.IsNullOrWhiteSpace(edit.Username) || string.IsNullOrWhiteSpace(edit.SiteId)))
            throw new ArgumentException("启用同步前请填写 BLUETTI 用户名和站点 ID。");
    }

    public async Task<ReceivingSyncSettings> Settings(int domain, CancellationToken ct)
    {
        await using var c = await connections.OpenConnectionAsync(ct); using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM receiving_sync_settings WHERE domain_id=@domain"; cmd.Parameters.AddWithValue("@domain", domain);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return new(false, "", false, "", 1800, false, 1800, 0, null, null, null, "", 0, 0);
        DateTime? Date(string name) => r.IsDBNull(r.GetOrdinal(name)) ? null : DateTime.SpecifyKind(r.GetDateTime(name), DateTimeKind.Utc);
        return new(r.GetBoolean("enabled"), r.GetString("username"), r.GetString("password_protected").Length > 0, r.GetString("site_id"), r.GetInt32("interval_seconds"),
            r.GetBoolean("auto_track"), r.GetInt32("track_interval_seconds"), r.GetInt32("revision"), Date("next_run_at"), Date("last_attempt_at"), Date("last_success_at"),
            r.GetString("last_error"), r.GetInt32("last_row_count"), r.GetInt32("last_issue_count"));
    }

    public async Task Save(int domain, string user, ReceivingSyncEdit edit, CancellationToken ct)
    {
        Validate(edit);
        await using var c = await connections.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        using var cmd = c.CreateCommand(); cmd.Transaction = tx; cmd.Parameters.AddWithValue("@domain", domain);
        cmd.CommandText = "SELECT revision,password_protected,username FROM receiving_sync_settings WHERE domain_id=@domain FOR UPDATE";
        int revision = 0; string secret = "", username = "";
        await using (var r = await cmd.ExecuteReaderAsync(ct)) if (await r.ReadAsync(ct)) { revision = r.GetInt32(0); secret = r.GetString(1); username = r.GetString(2); }
        if (revision != edit.Revision) throw new ApiException(409, "配置已更新，请重新加载。");
        if (username != edit.Username.Trim() && secret.Length > 0 && string.IsNullOrEmpty(edit.Password) && !edit.ClearPassword)
            throw new ArgumentException("更换用户名时请同时填写对应密码或清除原密码。");
        if (edit.ClearPassword) secret = "";
        else if (!string.IsNullOrEmpty(edit.Password)) secret = Protector(domain).Protect(edit.Password);
        if (edit.Enabled && secret.Length == 0) throw new ArgumentException("启用前必须填写 BLUETTI 密码。");
        cmd.Parameters.AddWithValue("@enabled", edit.Enabled); cmd.Parameters.AddWithValue("@username", edit.Username.Trim()); cmd.Parameters.AddWithValue("@secret", secret);
        cmd.Parameters.AddWithValue("@site", edit.SiteId.Trim()); cmd.Parameters.AddWithValue("@interval", edit.IntervalSeconds);
        cmd.Parameters.AddWithValue("@track", edit.AutoTrack); cmd.Parameters.AddWithValue("@trackInterval", edit.TrackIntervalSeconds);
        cmd.Parameters.AddWithValue("@user", user); cmd.Parameters.AddWithValue("@revision", revision);
        cmd.CommandText = revision == 0 ? """
            INSERT INTO receiving_sync_settings(domain_id,enabled,username,password_protected,site_id,interval_seconds,auto_track,track_interval_seconds,next_run_at,updated_by,updated_at)
            VALUES(@domain,@enabled,@username,@secret,@site,@interval,@track,@trackInterval,UTC_TIMESTAMP(3),@user,UTC_TIMESTAMP(3))
            """ : """
            UPDATE receiving_sync_settings SET current_run=IF(site_id=@site,current_run,''),last_success_at=IF(site_id=@site,last_success_at,NULL),
            last_row_count=IF(site_id=@site,last_row_count,0),last_issue_count=IF(site_id=@site,last_issue_count,0),
            enabled=@enabled,username=@username,password_protected=@secret,site_id=@site,interval_seconds=@interval,
            auto_track=@track,track_interval_seconds=@trackInterval,revision=revision+1,next_run_at=UTC_TIMESTAMP(3),updated_by=@user,updated_at=UTC_TIMESTAMP(3)
            WHERE domain_id=@domain AND revision=@revision
            """;
        try { if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new ApiException(409, "配置已更新，请重新加载。"); }
        catch (MySqlException ex) when (ex.Number is 1062 or 1213) { throw new ApiException(409, "配置并发修改，请重新加载。"); }
        await tx.CommitAsync(ct);
    }

    public async Task Queue(int domain, CancellationToken ct)
    {
        await using var c = await connections.OpenConnectionAsync(ct); using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE receiving_sync_settings SET next_run_at=UTC_TIMESTAMP(3) WHERE domain_id=@domain AND enabled=1";
        cmd.Parameters.AddWithValue("@domain", domain);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new ApiException(409, "请先保存配置并启用客户同步。");
    }

    public async Task<List<int>> Domains(CancellationToken ct)
    {
        await using var c = await connections.OpenConnectionAsync(ct); using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT domain_id FROM receiving_sync_settings WHERE enabled=1 OR auto_track=1";
        await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<int>(); while (await r.ReadAsync(ct)) result.Add(r.GetInt32(0)); return result;
    }

    // Connection-scoped MySQL locks protect exports across IIS recycling and multiple workers.
    public static async Task<bool> Lock(MySqlConnection c, string name, CancellationToken ct)
    {
        using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT GET_LOCK(@name,0)"; cmd.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
    }
    public static async Task Unlock(MySqlConnection c, string name)
    {
        using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT RELEASE_LOCK(@name)"; cmd.Parameters.AddWithValue("@name", name);
        await cmd.ExecuteScalarAsync(CancellationToken.None);
    }

    public async Task Sync(int domain, BluettiReceivingClient remote, CancellationToken ct)
    {
        await using var c = await connections.OpenConnectionAsync(ct); string key = "scanner:bluetti:" + c.Database + ":" + domain;
        // Bound lock names on older MySQL installations.
        key = "bluetti:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)))[..48];
        if (!await Lock(c, key, ct)) return;
        try
        {
            using var cmd = c.CreateCommand(); cmd.Parameters.AddWithValue("@domain", domain);
            cmd.CommandText = """
                SELECT s.username,s.password_protected,s.site_id,s.revision
                FROM receiving_sync_settings s JOIN user_domains d ON d.id=s.domain_id
                WHERE s.domain_id=@domain AND d.domain_name=@bluetti AND d.is_enabled=1
                  AND s.enabled=1 AND s.next_run_at<=UTC_TIMESTAMP(3)
                """;
            cmd.Parameters.AddWithValue("@bluetti", BluettiDomainName);
            string username, encrypted, site; int revision;
            await using (var r = await cmd.ExecuteReaderAsync(ct))
            {
                if (!await r.ReadAsync(ct)) return;
                username = r.GetString(0); encrypted = r.GetString(1); site = r.GetString(2); revision = r.GetInt32(3);
            }
            string run = Guid.NewGuid().ToString("D"); cmd.Parameters.AddWithValue("@run", run); cmd.Parameters.AddWithValue("@site", site); cmd.Parameters.AddWithValue("@revision", revision);
            cmd.CommandText = "UPDATE receiving_sync_runs SET status='interrupted',finished_at=UTC_TIMESTAMP(3),message='上次运行被服务退出中断，已重新调度。' WHERE domain_id=@domain AND status='running'";
            await cmd.ExecuteNonQueryAsync(ct);
            cmd.CommandText = "INSERT INTO receiving_sync_runs(run_id,domain_id,site_id,started_at,status) VALUES(@run,@domain,@site,UTC_TIMESTAMP(3),'running')"; await cmd.ExecuteNonQueryAsync(ct);
            cmd.CommandText = "UPDATE receiving_sync_settings SET last_attempt_at=UTC_TIMESTAMP(3),next_run_at=DATE_ADD(UTC_TIMESTAMP(3),INTERVAL interval_seconds SECOND) WHERE domain_id=@domain AND revision=@revision";
            await cmd.ExecuteNonQueryAsync(ct);
            try
            {
                string password = Protector(domain).Unprotect(encrypted);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromMinutes(12));
                var rows = await remote.Fetch(username, password, site, deadline.Token);
                await using var tx = await c.BeginTransactionAsync(ct); cmd.Transaction = tx;
                foreach (var row in rows)
                {
                    using var insert = c.CreateCommand(); insert.Transaction = tx;
                    insert.CommandText = """
                        INSERT INTO bluetti_receiving_forecasts(domain_id,site_id,source_key,rma,carrier,tracking_number,issue,source_json,first_seen_at,last_seen_at,run_id)
                        VALUES(@domain,@site,@key,@rma,@carrier,@number,@issue,@json,UTC_TIMESTAMP(3),UTC_TIMESTAMP(3),@run)
                        ON DUPLICATE KEY UPDATE rma=@rma,issue=@issue,source_json=@json,last_seen_at=UTC_TIMESTAMP(3),run_id=@run
                        """;
                    insert.Parameters.AddWithValue("@domain", domain); insert.Parameters.AddWithValue("@site", site); insert.Parameters.AddWithValue("@key", row.Key);
                    insert.Parameters.AddWithValue("@rma", row.Rma); insert.Parameters.AddWithValue("@carrier", row.Carrier); insert.Parameters.AddWithValue("@number", row.Number);
                    insert.Parameters.AddWithValue("@issue", row.Issue); insert.Parameters.AddWithValue("@json", row.SourceJson); insert.Parameters.AddWithValue("@run", run);
                    await insert.ExecuteNonQueryAsync(ct);
                    if (row.Issue.Length != 0) continue;
                    insert.CommandText = """
                        INSERT INTO carrier_receiving(domain_id,carrier,tracking_number,reference,created_at,created_by)
                        VALUES(@domain,@carrier,@number,@rma,UTC_TIMESTAMP(3),'bluetti-sync')
                        ON DUPLICATE KEY UPDATE tracking_number=tracking_number
                        """;
                    await insert.ExecuteNonQueryAsync(ct);
                }
                cmd.Parameters.AddWithValue("@rows", rows.Count); cmd.Parameters.AddWithValue("@issues", rows.Count(r => r.Issue.Length > 0));
                cmd.CommandText = """
                    UPDATE receiving_sync_settings SET current_run=@run,last_success_at=UTC_TIMESTAMP(3),last_error='',last_row_count=@rows,last_issue_count=@issues,
                    next_run_at=DATE_ADD(UTC_TIMESTAMP(3),INTERVAL interval_seconds SECOND) WHERE domain_id=@domain AND revision=@revision AND enabled=1
                    """;
                if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new ApiException(409, "同步期间配置已更改，本批次未导入，将按新配置重试。");
                cmd.CommandText = "UPDATE receiving_sync_runs SET status='succeeded',finished_at=UTC_TIMESTAMP(3),row_count=@rows,issue_count=@issues WHERE run_id=@run";
                await cmd.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct); cmd.Transaction = null;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                cmd.Transaction = null;
                string message = ex is ApiException or InvalidDataException ? ex.Message : "BLUETTI 同步失败，请检查连接、凭据或加密密钥。";
                if (message.Length > 500) message = message[..500];
                cmd.Parameters.AddWithValue("@error", message);
                cmd.CommandText = "UPDATE receiving_sync_runs SET status='failed',finished_at=UTC_TIMESTAMP(3),message=@error WHERE run_id=@run"; await cmd.ExecuteNonQueryAsync(ct);
                cmd.CommandText = "UPDATE receiving_sync_settings SET last_error=@error,next_run_at=DATE_ADD(UTC_TIMESTAMP(3),INTERVAL interval_seconds SECOND) WHERE domain_id=@domain AND revision=@revision";
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        finally { await Unlock(c, key); }
    }

    public async Task<object> Forecasts(int domain, ulong before, CancellationToken ct)
    {
        await using var c = await connections.OpenConnectionAsync(ct); using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT f.id,f.rma,f.carrier,f.tracking_number,f.issue,f.last_seen_at FROM bluetti_receiving_forecasts f
            JOIN receiving_sync_settings s ON s.domain_id=f.domain_id AND s.current_run=f.run_id AND s.site_id=f.site_id
            WHERE f.domain_id=@domain AND (@before=0 OR f.id<@before) ORDER BY f.id DESC LIMIT 100
            """;
        cmd.Parameters.AddWithValue("@domain", domain); cmd.Parameters.AddWithValue("@before", before);
        await using var r = await cmd.ExecuteReaderAsync(ct); var rows = new List<object>();
        while (await r.ReadAsync(ct)) rows.Add(new { id = r.GetUInt64(0), rma = r.GetString(1), carrier = r.GetString(2), trackingNumber = r.GetString(3), issue = r.GetString(4), lastSeenAt = DateTime.SpecifyKind(r.GetDateTime(5), DateTimeKind.Utc) });
        return rows;
    }
}
