using Scanner.Server.Model;
namespace Scanner.Server.DAL;
public sealed class SkuSnBatchRepository(IMySqlConnectionFactory factory)
{
    public async Task SaveAsync(SkuSnBatch batch, CancellationToken ct)
    {
        await using var connection = await factory.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO sku_sn_batches (batch_id,batch_number,created_at,item_count) VALUES (@id,@number,@time,@count) ON DUPLICATE KEY UPDATE batch_id=batch_id;";
        cmd.Parameters.AddWithValue("@id", batch.BatchId.ToString("D"));
        cmd.Parameters.AddWithValue("@number", batch.BatchNumber);
        cmd.Parameters.AddWithValue("@time", batch.CreatedAt.UtcDateTime);
        cmd.Parameters.AddWithValue("@count", batch.Items.Count);
        await cmd.ExecuteNonQueryAsync(ct);
        cmd.CommandText = "SELECT batch_number,created_at,item_count FROM sku_sn_batches WHERE batch_id=@id FOR UPDATE;";
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            await reader.ReadAsync(ct);
            if (reader.GetString(0) != batch.BatchNumber || reader.GetDateTime(1) != batch.CreatedAt.UtcDateTime || reader.GetInt32(2) != batch.Items.Count)
                throw new ArgumentException("批次 GUID 已用于其他批次内容。");
        }
        var existing = new List<SkuSnItem>();
        cmd.CommandText = "SELECT sku,sn FROM sku_sn_batch_items WHERE batch_id=@id ORDER BY line_number;";
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) existing.Add(new(reader.GetString(0), reader.GetString(1)));
        if (existing.Count > 0)
        {
            if (!existing.SequenceEqual(batch.Items)) throw new ArgumentException("批次已上传，不能修改内容。");
        }
        else
        {
            for (int i = 0; i < batch.Items.Count; i++)
            {
                cmd.CommandText = "INSERT INTO sku_sn_batch_items (batch_id,line_number,sku,sn) VALUES (@id,@line,@sku,@sn);";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@id", batch.BatchId.ToString("D"));
                cmd.Parameters.AddWithValue("@line", i + 1);
                cmd.Parameters.AddWithValue("@sku", batch.Items[i].Sku);
                cmd.Parameters.AddWithValue("@sn", batch.Items[i].Sn);
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        await tx.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<SkuSnBatchSummary>> ListAsync(CancellationToken ct)
    {
        await using var connection = await factory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT batch_id,batch_number,created_at,item_count FROM sku_sn_batches ORDER BY created_at DESC,batch_id;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<SkuSnBatchSummary>();
        while (await reader.ReadAsync(ct)) rows.Add(new(Guid.Parse(reader.GetString(0)),reader.GetString(1),new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(2),DateTimeKind.Utc)),reader.GetInt32(3)));
        return rows;
    }
    public async Task<SkuSnBatch?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var connection = await factory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT batch_number,created_at FROM sku_sn_batches WHERE batch_id=@id;";
        cmd.Parameters.AddWithValue("@id",id.ToString("D"));
        string number; DateTimeOffset time;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return null;
            number = reader.GetString(0); time = new(DateTime.SpecifyKind(reader.GetDateTime(1),DateTimeKind.Utc));
        }
        cmd.CommandText = "SELECT sku,sn FROM sku_sn_batch_items WHERE batch_id=@id ORDER BY line_number;";
        var items = new List<SkuSnItem>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) items.Add(new(reader.GetString(0),reader.GetString(1)));
        return new(id,number,time,items);
    }
}
