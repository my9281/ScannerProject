using MySqlConnector;
using Scanner.Server.Model;
using System.Globalization;

namespace Scanner.Server.DAL;

public sealed class ShelvedPalletRepository(IMySqlConnectionFactory connectionFactory) : IShelvedPalletRepository
{
    public async Task<ShelvedPalletTableResult> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM `shelved_pallet_data` ORDER BY `shelving_date` DESC, `shelving_time` DESC, `number` ASC;";
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        string[] columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        List<IReadOnlyDictionary<string, object?>> rows = new();
        while (await reader.ReadAsync(cancellationToken))
        {
            Dictionary<string, object?> row = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < reader.FieldCount; index++)
            {
                object? value = await reader.IsDBNullAsync(index, cancellationToken) ? null : reader.GetValue(index);
                row[columns[index]] = value switch
                {
                    TimeSpan time => time.ToString(@"hh\:mm\:ss"),
                    byte[] bytes => Convert.ToHexString(bytes),
                    _ => value
                };
            }
            rows.Add(row);
        }
        return new ShelvedPalletTableResult(columns, rows, rows.Count);
    }

    public async Task<IReadOnlyList<ShelvedPalletRecord>> CreateBatchAsync(
        string palletNumber,
        DateTime shelvedAt,
        IReadOnlyList<ShelvedPalletItemRequest> items,
        CancellationToken cancellationToken = default, Guid? requestId = null)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        List<ShelvedPalletRecord> created = new(items.Count);
        try
        {
            if (requestId.HasValue)
            {
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new { palletNumber, items }))));
                await using var claim = connection.CreateCommand();
                claim.Transaction = transaction;
                claim.Parameters.AddWithValue("@request", requestId.Value.ToString("D"));
                claim.Parameters.AddWithValue("@hash", hash);
                claim.CommandText = "INSERT INTO shelving_requests (request_id,content_hash) VALUES (@request,@hash) ON DUPLICATE KEY UPDATE request_id=request_id;";
                await claim.ExecuteNonQueryAsync(cancellationToken);
                claim.CommandText = "SELECT content_hash,result_json FROM shelving_requests WHERE request_id=@request FOR UPDATE;";
                string? saved;
                await using (var reader = await claim.ExecuteReaderAsync(cancellationToken))
                {
                    await reader.ReadAsync(cancellationToken);
                    if (reader.GetString(0) != hash) throw new ArgumentException("上架请求标识已用于不同内容。");
                    saved = reader.IsDBNull(1) ? null : reader.GetString(1);
                }
                if (saved is not null)
                {
                    var prior = System.Text.Json.JsonSerializer.Deserialize<List<ShelvedPalletRecord>>(saved)
                        ?? throw new InvalidOperationException("上架请求回执损坏。");
                    await transaction.CommitAsync(cancellationToken);
                    return prior.Select(row => row with { IsReplay = true }).ToArray();
                }
            }
            foreach (ShelvedPalletItemRequest item in items.OrderBy(x => x.Sn, StringComparer.OrdinalIgnoreCase))
            {
                string uuid = Guid.NewGuid().ToString();
                DateTime? processingTime = ParseProcessingTime(item.ProcessingTime);
                await using MySqlCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO `shelved_pallet_data`
                        (`uuid`, `number`, `sn`, `sku`, `type`, `processing_method`, `processing_time`, `shelving_date`, `pallet_number`, `shelving_time`)
                    VALUES
                        (@uuid, @number, @sn, @sku, @type, @processingMethod, @processingTime, @shelvingDate, @palletNumber, @shelvingTime);
                    """;
                command.Parameters.Add("@uuid", MySqlDbType.VarChar, 36).Value = uuid;
                command.Parameters.Add("@number", MySqlDbType.Int32).Value = item.Number;
                command.Parameters.Add("@sn", MySqlDbType.VarChar, 100).Value = item.Sn;
                command.Parameters.Add("@sku", MySqlDbType.VarChar, 150).Value = item.Sku;
                command.Parameters.Add("@type", MySqlDbType.VarChar, 100).Value = DbValue(item.Type);
                command.Parameters.Add("@processingMethod", MySqlDbType.VarChar, 100).Value = DbValue(item.ProcessingMethod);
                command.Parameters.Add("@processingTime", MySqlDbType.DateTime).Value = processingTime.HasValue ? processingTime.Value : DBNull.Value;
                command.Parameters.Add("@shelvingDate", MySqlDbType.Date).Value = shelvedAt.Date;
                command.Parameters.Add("@palletNumber", MySqlDbType.VarChar, 100).Value = palletNumber;
                command.Parameters.Add("@shelvingTime", MySqlDbType.Time).Value = shelvedAt.TimeOfDay;
                await command.ExecuteNonQueryAsync(cancellationToken);
                command.CommandText = "INSERT INTO inventory_device_state (sn,cycle_number,is_shelved) VALUES (@sn,1,0) ON DUPLICATE KEY UPDATE sn=sn;";
                await command.ExecuteNonQueryAsync(cancellationToken);
                command.CommandText = "SELECT cycle_number FROM inventory_device_state WHERE sn=@sn FOR UPDATE;";
                var cycle = await command.ExecuteScalarAsync(cancellationToken);
                command.Parameters.AddWithValue("@cycle", cycle);
                command.CommandText = "UPDATE inventory_device_state SET is_shelved=1 WHERE sn=@sn;";
                await command.ExecuteNonQueryAsync(cancellationToken);
                // Use the same connection and transaction: shelving and removal must commit together.
                command.CommandText = "UPDATE `checklist_work_orders` SET `audit_status`=1, `version`=`version`+1 WHERE `device_id`=@sn COLLATE utf8mb4_unicode_ci AND `cycle_number`=@cycle AND `audit_status`=0;";
                await command.ExecuteNonQueryAsync(cancellationToken);
                command.CommandText = "UPDATE `tester_pallet_scans` SET `is_archived`=1, `archived_reason`='shelved', `archived_at`=UTC_TIMESTAMP(3) WHERE `sn` COLLATE utf8mb4_unicode_ci = @sn AND `is_archived`=0;";
                await command.ExecuteNonQueryAsync(cancellationToken);
                created.Add(new ShelvedPalletRecord(uuid, item.Number, item.Sn, item.Sku, item.Type, item.ProcessingMethod,
                    processingTime, DateOnly.FromDateTime(shelvedAt), palletNumber, TimeOnly.FromDateTime(shelvedAt)));
            }

            if (requestId.HasValue)
            {
                await using var receipt = connection.CreateCommand();
                receipt.Transaction = transaction;
                receipt.CommandText = "UPDATE shelving_requests SET result_json=@result WHERE request_id=@request;";
                receipt.Parameters.AddWithValue("@request", requestId.Value.ToString("D"));
                receipt.Parameters.AddWithValue("@result", System.Text.Json.JsonSerializer.Serialize(created));
                await receipt.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return created;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<ShelvedPalletRecord>> QueryAsync(
        DateTime? from,
        DateTime? to,
        string? palletNumber,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlCommand command = connection.CreateCommand();
        List<string> conditions = new();
        if (from.HasValue)
        {
            conditions.Add("(`shelving_date` > DATE(@from) OR (`shelving_date` = DATE(@from) AND `shelving_time` >= TIME(@from)))");
            command.Parameters.Add("@from", MySqlDbType.DateTime).Value = from.Value;
        }
        if (to.HasValue)
        {
            conditions.Add("(`shelving_date` < DATE(@to) OR (`shelving_date` = DATE(@to) AND `shelving_time` <= TIME(@to)))");
            command.Parameters.Add("@to", MySqlDbType.DateTime).Value = to.Value;
        }
        if (!string.IsNullOrWhiteSpace(palletNumber))
        {
            conditions.Add("`pallet_number` = @palletNumber");
            command.Parameters.Add("@palletNumber", MySqlDbType.VarChar, 100).Value = palletNumber;
        }

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        command.CommandText = "SELECT `uuid`, `number`, `sn`, `sku`, `type`, `processing_method`, `processing_time`, `shelving_date`, `pallet_number`, `shelving_time` " +
            "FROM `shelved_pallet_data`" + where + " ORDER BY `shelving_date` DESC, `shelving_time` DESC, `number` ASC LIMIT @limit;";
        command.Parameters.Add("@limit", MySqlDbType.Int32).Value = limit;

        List<ShelvedPalletRecord> result = new();
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(Read(reader));
        return result;
    }

    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static DateTime? ParseProcessingTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime invariant)
            || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out invariant)
            ? invariant
            : null;
    }

    private static ShelvedPalletRecord Read(MySqlDataReader reader) => new(
        reader.GetString("uuid"),
        reader.GetInt32("number"),
        reader.GetString("sn"),
        reader.GetString("sku"),
        reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString("type"),
        reader.IsDBNull(reader.GetOrdinal("processing_method")) ? null : reader.GetString("processing_method"),
        reader.IsDBNull(reader.GetOrdinal("processing_time")) ? null : reader.GetDateTime("processing_time"),
        DateOnly.FromDateTime(reader.GetDateTime("shelving_date")),
        reader.GetString("pallet_number"),
        TimeOnly.FromTimeSpan(reader.GetTimeSpan("shelving_time")));
}
