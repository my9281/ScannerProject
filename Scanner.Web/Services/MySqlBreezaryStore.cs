using MySqlConnector;
using Scanner.Server.DAL;
using System.Text.Json.Nodes;

namespace Scanner.Web.Services;

public sealed class MySqlBreezaryStore(IMySqlConnectionFactory connections) : IBreezaryStore
{
    private const string Columns = "record_id,file_id,sheet_id,remote_values_json,pending_values_json,base_values_json,sync_status,last_error,version,last_pulled_at,last_pushed_at";

    public Task<IAsyncDisposable> AcquireAsync(string target, CancellationToken ct) => RunAsync<IAsyncDisposable>(async () =>
    {
        var connection = await connections.OpenConnectionAsync(ct);
        var name = "breezary:" + target[..40];
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT GET_LOCK(@name,0)";
            command.Parameters.AddWithValue("@name", name);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
                throw new ApiException(409, "已有同步或编辑操作正在执行，请稍后重试。");
            return new Lease(connection, name);
        }
        catch { await connection.DisposeAsync(); throw; }
    });

    public Task<IReadOnlyList<BreezaryRow>> ReadAllAsync(string target, CancellationToken ct) => RunAsync<IReadOnlyList<BreezaryRow>>(async () =>
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM breezarystar_receipts WHERE target_key=@target ORDER BY id";
        command.Parameters.AddWithValue("@target", target);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<BreezaryRow>();
        while (await reader.ReadAsync(ct)) rows.Add(Read(reader));
        return rows;
    });

    public Task<BreezaryLocalPage> ListAsync(string target, int offset, int limit, CancellationToken ct) => RunAsync(async () =>
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM breezarystar_receipts WHERE target_key=@target";
        count.Parameters.AddWithValue("@target", target);
        var total = Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM breezarystar_receipts WHERE target_key=@target ORDER BY id LIMIT @limit OFFSET @offset";
        command.Parameters.AddWithValue("@target", target);
        command.Parameters.AddWithValue("@limit", limit);
        command.Parameters.AddWithValue("@offset", offset);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var records = new JsonArray();
        while (await reader.ReadAsync(ct)) records.Add(BreezaryMerge.ToJson(Read(reader)));
        var more = offset + records.Count < total;
        return new BreezaryLocalPage(records, more, more ? offset + records.Count : 0, total);
    });

    public async Task SaveAsync(string target, IReadOnlyList<BreezaryRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;
        await RunAsync(async () =>
        {
            await using var connection = await connections.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            foreach (var row in rows)
            {
                if (row.Version < 1 || row.FileId.Length > 255 || row.SheetId.Length > 128 || row.RecordId.Length is 0 or > 128)
                    throw new ApiException(400, "记录标识长度或版本无效。");
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = row.Version == 1 ? """
                    INSERT INTO breezarystar_receipts
                    (target_key,record_id,file_id,sheet_id,remote_values_json,pending_values_json,base_values_json,sync_status,last_error,
                     version,last_pulled_at,last_pushed_at,updated_at,work_order_no,sku,tracking_no,actual_tracking_no,channel,receipt_status,workflow_status,ship_date_ms,receipt_date_ms,remark,issue_description)
                    VALUES (@target,@record,@file,@sheet,@remote,@pending,@baseline,@status,@error,@version,@pulled,@pushed,@now,
                     @order,@sku,@tracking,@actualTracking,@channel,@receipt,@workflow,@date,@receiptDate,@remark,@issue)
                    """ : """
                    UPDATE breezarystar_receipts SET remote_values_json=@remote,pending_values_json=@pending,base_values_json=@baseline,
                     sync_status=@status,last_error=@error,version=@version,last_pulled_at=@pulled,last_pushed_at=@pushed,updated_at=@now,
                     work_order_no=@order,sku=@sku,tracking_no=@tracking,actual_tracking_no=@actualTracking,channel=@channel,receipt_status=@receipt,
                     workflow_status=@workflow,ship_date_ms=@date,receipt_date_ms=@receiptDate,remark=@remark,issue_description=@issue
                    WHERE target_key=@target AND record_id=@record AND version=@previous
                    """;
                command.Parameters.AddWithValue("@target", target);
                command.Parameters.AddWithValue("@record", row.RecordId);
                command.Parameters.AddWithValue("@file", row.FileId);
                command.Parameters.AddWithValue("@sheet", row.SheetId);
                command.Parameters.AddWithValue("@remote", row.Remote.ToJsonString());
                command.Parameters.AddWithValue("@pending", row.Pending.ToJsonString());
                command.Parameters.AddWithValue("@baseline", row.Baseline.ToJsonString());
                command.Parameters.AddWithValue("@status", row.SyncStatus);
                command.Parameters.AddWithValue("@error", row.LastError);
                command.Parameters.AddWithValue("@version", row.Version);
                command.Parameters.AddWithValue("@previous", row.Version - 1);
                command.Parameters.AddWithValue("@pulled", row.LastPulledAt);
                command.Parameters.AddWithValue("@pushed", row.LastPushedAt);
                command.Parameters.AddWithValue("@now", DateTime.UtcNow);
                var values = row.Values;
                string Text(string name, int max) { var text = BreezaryMerge.Text(values[name]); return text[..Math.Min(text.Length, max)]; }
                command.Parameters.AddWithValue("@order", Text("工单号", 160));
                command.Parameters.AddWithValue("@sku", Text("SKU", 160));
                command.Parameters.AddWithValue("@tracking", Text("运单号", 160));
                command.Parameters.AddWithValue("@actualTracking", Text("实际签收快递单号", 160));
                command.Parameters.AddWithValue("@channel", Text("渠道", 64));
                command.Parameters.AddWithValue("@receipt", Text("是否签收", 64));
                command.Parameters.AddWithValue("@workflow", Text("状态流转", 160));
                command.Parameters.AddWithValue("@date", long.TryParse(BreezaryMerge.Text(values["发货日期"]), out var ms) ? ms : (object)DBNull.Value);
                command.Parameters.AddWithValue("@receiptDate", long.TryParse(BreezaryMerge.Text(values["签收日期"]), out var receiptMs) ? receiptMs : (object)DBNull.Value);
                command.Parameters.AddWithValue("@remark", Text("备注", 10000));
                command.Parameters.AddWithValue("@issue", Text("问题描述", 10000));
                if (await command.ExecuteNonQueryAsync(ct) != 1) throw new ApiException(409, "本地记录版本已变化，请刷新后重试。");
            }
            await transaction.CommitAsync(ct);
            return 0;
        });
    }

    private static BreezaryRow Read(MySqlDataReader reader) => new()
    {
        RecordId = reader.GetString("record_id"), FileId = reader.GetString("file_id"), SheetId = reader.GetString("sheet_id"),
        Remote = JsonNode.Parse(reader.GetString("remote_values_json"))!.AsObject(),
        Pending = JsonNode.Parse(reader.GetString("pending_values_json"))!.AsObject(),
        Baseline = JsonNode.Parse(reader.GetString("base_values_json"))!.AsObject(),
        SyncStatus = reader.GetString("sync_status"), LastError = reader.GetString("last_error"), Version = reader.GetInt64("version"),
        LastPulledAt = reader.IsDBNull(reader.GetOrdinal("last_pulled_at")) ? null : DateTime.SpecifyKind(reader.GetDateTime("last_pulled_at"), DateTimeKind.Utc),
        LastPushedAt = reader.IsDBNull(reader.GetOrdinal("last_pushed_at")) ? null : DateTime.SpecifyKind(reader.GetDateTime("last_pushed_at"), DateTimeKind.Utc)
    };

    private static async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (MySqlException ex) when (ex.Number == 1146)
        { throw new ApiException(503, "本地表尚未创建，请先执行 Database/017_breezarystar_receipts.sql。"); }
        catch (MySqlException ex) when (ex.Number == 1054)
        { throw new ApiException(503, "本地表缺少业务字段，请执行 Database/018_breezarystar_receipt_fields.sql。"); }
        catch (MySqlException ex) when (ex.Number == 1062)
        { throw new ApiException(409, "记录已存在，请刷新后重新同步。"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("MySQL access is disabled", StringComparison.Ordinal))
        { throw new ApiException(503, "本地数据库未启用，请配置 Database:Enabled 和连接参数。"); }
    }

    private sealed class Lease(MySqlConnection connection, string name) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT RELEASE_LOCK(@name)";
                command.CommandTimeout = 5;
                command.Parameters.AddWithValue("@name", name);
                await command.ExecuteScalarAsync();
            }
            catch (MySqlException) { MySqlConnection.ClearPool(connection); }
            finally { await connection.DisposeAsync(); }
        }
    }
}
