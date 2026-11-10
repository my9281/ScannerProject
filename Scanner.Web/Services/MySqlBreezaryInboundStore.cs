using MySqlConnector;
using Scanner.Server.DAL;
using Scanner.Server.Model;

namespace Scanner.Web.Services;

public sealed class MySqlBreezaryInboundStore(IMySqlConnectionFactory connections) : IBreezaryInboundStore
{
    public async Task SaveAsync(IReadOnlyList<BreezaryInboundItem> items, CancellationToken ct)
    {
        await ExecuteAsync(async () =>
        {
            await using var connection = await connections.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            foreach (var item in items)
            {
                using var insert = connection.CreateCommand(); insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO breezarystar_inbound_scans (scan_id,device_id,cache_location,barcode,scanned_at,uploaded_at)
                    VALUES (@scan,@device,@location,@barcode,@time,@uploaded)
                    ON DUPLICATE KEY UPDATE scan_id=VALUES(scan_id)
                    """;
                insert.Parameters.AddWithValue("@scan", item.ScanId.ToString("D"));
                insert.Parameters.AddWithValue("@device", item.DeviceId.ToString("D"));
                insert.Parameters.AddWithValue("@location", item.CacheLocation);
                insert.Parameters.AddWithValue("@barcode", item.Barcode);
                insert.Parameters.AddWithValue("@time", DateTimeOffset.FromUnixTimeMilliseconds(item.ScannedAt.ToUnixTimeMilliseconds()).UtcDateTime);
                insert.Parameters.AddWithValue("@uploaded", DateTime.UtcNow);
                await insert.ExecuteNonQueryAsync(ct);
                // Verify duplicates against the complete immutable payload, never overwrite a previous scan.
                using var verify = connection.CreateCommand(); verify.Transaction = transaction;
                verify.CommandText = "SELECT device_id,cache_location,barcode,scanned_at FROM breezarystar_inbound_scans WHERE scan_id=@scan FOR UPDATE";
                verify.Parameters.AddWithValue("@scan", item.ScanId.ToString("D"));
                await using var reader = await verify.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct) || reader.GetGuid("device_id") != item.DeviceId
                    || reader.GetInt32("cache_location") != item.CacheLocation || reader.GetString("barcode") != item.Barcode
                    || new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime("scanned_at"), DateTimeKind.Utc)).ToUnixTimeMilliseconds() != item.ScannedAt.ToUnixTimeMilliseconds())
                    throw new ApiException(409, "同一扫描 ID 对应的内容不一致，此批次未确认，请联系管理员。");
            }
            await transaction.CommitAsync(ct);
            return 0;
        });
    }

    public Task<BreezaryInboundPage> ListAsync(ulong beforeId, int limit, int? location, string? search, CancellationToken ct) => ExecuteAsync(async () =>
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,scan_id,device_id,cache_location,barcode,scanned_at,uploaded_at FROM breezarystar_inbound_scans
            WHERE (@before=0 OR id<@before) AND (@location IS NULL OR cache_location=@location)
              AND (@search='' OR LOCATE(@search,barcode)>0)
            ORDER BY id DESC LIMIT @limit
            """;
        command.Parameters.AddWithValue("@before", beforeId);
        command.Parameters.AddWithValue("@location", location);
        command.Parameters.AddWithValue("@search", search ?? "");
        command.Parameters.AddWithValue("@limit", limit + 1);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<BreezaryInboundStored>();
        while (await reader.ReadAsync(ct))
            rows.Add(new(reader.GetUInt64("id"), reader.GetGuid("scan_id"), reader.GetGuid("device_id"),
                reader.GetInt32("cache_location"), reader.GetString("barcode"),
                new(DateTime.SpecifyKind(reader.GetDateTime("scanned_at"), DateTimeKind.Utc)),
                new(DateTime.SpecifyKind(reader.GetDateTime("uploaded_at"), DateTimeKind.Utc))));
        bool more = rows.Count > limit;
        if (more) rows.RemoveAt(rows.Count - 1);
        return new BreezaryInboundPage(rows, more, more ? rows[^1].Id : 0);
    });

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (MySqlException ex) when (ex.Number == 1146)
        { throw new ApiException(503, "请先执行 Database/019_breezarystar_inbound_scans.sql 创建扫码入库表。"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("MySQL access is disabled", StringComparison.Ordinal))
        { throw new ApiException(503, "请先启用服务器 Database 配置。"); }
    }
}
