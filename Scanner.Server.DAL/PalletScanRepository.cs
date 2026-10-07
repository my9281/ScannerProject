using MySqlConnector;
using Scanner.Server.Model;
namespace Scanner.Server.DAL;

public sealed class PalletScanRepository(IMySqlConnectionFactory factory) : IPalletScanRepository
{
    public async Task<IReadOnlyList<PalletScanStoredRow>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sn, pallet_number, scan_date, scan_time FROM tester_pallet_scans ORDER BY pallet_number, scan_time, sn;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<PalletScanStoredRow>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetDateTime(2), reader.GetDateTime(3)));
        return rows;
    }
    public async Task SaveAsync(IReadOnlyList<PalletScanItem> items, CancellationToken cancellationToken = default)
    {
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // Stable ordering avoids conflicting lock order when batches overlap on retry.
        foreach (var item in items.OrderBy(x => x.Sn, StringComparer.Ordinal))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO `tester_pallet_scans` (`scan_id`, `sn`, `scan_date`, `pallet_number`, `scan_time`)
                VALUES (@scanId, @sn, @date, @pallet, @time)
                ON DUPLICATE KEY UPDATE
                    `scan_date` = @date,
                    `pallet_number` = @pallet,
                    `scan_time` = @time;
                """;
            command.Parameters.Add("@scanId", MySqlDbType.VarChar, 36).Value = item.ScanId.ToString("D");
            command.Parameters.Add("@sn", MySqlDbType.VarChar, 100).Value = item.Sn;
            command.Parameters.Add("@date", MySqlDbType.Date).Value = item.ScannedAt.Date;
            command.Parameters.Add("@pallet", MySqlDbType.Int32).Value = item.PalletNumber;
            command.Parameters.Add("@time", MySqlDbType.DateTime).Value = item.ScannedAt.DateTime;
            var upsertSql = command.CommandText;
            command.CommandText = "SELECT `sn` FROM `tester_pallet_scans` WHERE `scan_id` = @scanId FOR UPDATE;";
            var existingSn = await command.ExecuteScalarAsync(cancellationToken);
            if (existingSn is string sn && sn != item.Sn)
                throw new ArgumentException("扫描标识已用于其他 SN。");
            command.CommandText = upsertSql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
