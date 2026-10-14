using MySqlConnector;
using Scanner.Server.Model;
namespace Scanner.Server.DAL;

public sealed class PalletScanRepository(IMySqlConnectionFactory factory) : IPalletScanRepository
{
    public async Task<IReadOnlyList<PalletScanStoredRow>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT p.sn, p.pallet_number, p.scan_date, p.scan_time, s.cycle_number FROM tester_pallet_scans p INNER JOIN inventory_device_state s ON s.sn=p.sn COLLATE utf8mb4_unicode_ci WHERE p.is_archived=0 ORDER BY p.pallet_number, p.scan_time, p.sn;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<PalletScanStoredRow>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetDateTime(2), reader.GetDateTime(3), reader.GetUInt64(4)));
        return rows;
    }
    public async Task SaveAsync(IReadOnlyList<PalletScanItem> items, CancellationToken cancellationToken = default)
    {
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // Stable ordering avoids conflicting lock order when batches overlap on retry.
        foreach (var item in items.OrderBy(x => x.Sn, StringComparer.OrdinalIgnoreCase))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO `tester_pallet_scans` (`scan_id`, `sn`, `scan_date`, `pallet_number`, `scan_time`)
                VALUES (@scanId, @sn, @date, @pallet, @time)
                ON DUPLICATE KEY UPDATE
                    `pallet_number` = IF(`is_archived`=0 AND @time>=`scan_time`,@pallet,`pallet_number`),
                    `scan_date` = IF(`is_archived`=0 AND @time>=`scan_time`,@date,`scan_date`),
                    `scan_time` = IF(`is_archived`=0 AND @time>=`scan_time`,@time,`scan_time`);
                """;
            command.Parameters.Add("@scanId", MySqlDbType.VarChar, 36).Value = item.ScanId.ToString("D");
            command.Parameters.Add("@sn", MySqlDbType.VarChar, 100).Value = item.Sn;
            command.Parameters.Add("@date", MySqlDbType.Date).Value = item.ScannedAt.Date;
            command.Parameters.Add("@pallet", MySqlDbType.Int32).Value = item.PalletNumber;
            command.Parameters.Add("@time", MySqlDbType.DateTime).Value = item.ScannedAt.DateTime;
            var upsertSql = command.CommandText;
            string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(item))));
            command.Parameters.AddWithValue("@hash", hash);
            command.CommandText = "INSERT INTO pallet_scan_receipts (scan_id,sn,content_hash) VALUES (@scanId,@sn,@hash) ON DUPLICATE KEY UPDATE scan_id=scan_id;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "SELECT sn,content_hash,is_processed FROM pallet_scan_receipts WHERE scan_id=@scanId FOR UPDATE;";
            bool processed;
            await using (var receipt = await command.ExecuteReaderAsync(cancellationToken))
            {
                await receipt.ReadAsync(cancellationToken);
                if (receipt.GetString(0) != item.Sn || (!receipt.IsDBNull(1) && receipt.GetString(1) != hash))
                    throw new ArgumentException("扫描标识已用于不同扫描内容。");
                processed = receipt.GetBoolean(2);
            }
            if (processed) continue;
            command.CommandText = "INSERT INTO inventory_device_state (sn,cycle_number,is_shelved) VALUES (@sn,1,0) ON DUPLICATE KEY UPDATE sn=sn;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "SELECT cycle_number FROM inventory_device_state WHERE sn=@sn FOR UPDATE;";
            await command.ExecuteScalarAsync(cancellationToken);
            command.CommandText = "UPDATE inventory_device_state SET cycle_number=cycle_number+IF(is_shelved=1,1,0),is_shelved=0 WHERE sn=@sn;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "SELECT `sn` FROM `tester_pallet_scans` WHERE `scan_id` = @scanId FOR UPDATE;";
            var existingSn = await command.ExecuteScalarAsync(cancellationToken);
            if (existingSn is string sn && sn != item.Sn)
                throw new ArgumentException("扫描标识已用于其他 SN。");
            command.CommandText = upsertSql;
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "UPDATE pallet_scan_receipts SET is_processed=1 WHERE scan_id=@scanId;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
