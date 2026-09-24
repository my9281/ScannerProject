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
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        List<ShelvedPalletRecord> created = new(items.Count);
        try
        {
            foreach (ShelvedPalletItemRequest item in items)
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
                created.Add(new ShelvedPalletRecord(uuid, item.Number, item.Sn, item.Sku, item.Type, item.ProcessingMethod,
                    processingTime, DateOnly.FromDateTime(shelvedAt), palletNumber, TimeOnly.FromDateTime(shelvedAt)));
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
            conditions.Add("TIMESTAMP(`shelving_date`, `shelving_time`) >= @from");
            command.Parameters.Add("@from", MySqlDbType.DateTime).Value = from.Value;
        }
        if (to.HasValue)
        {
            conditions.Add("TIMESTAMP(`shelving_date`, `shelving_time`) <= @to");
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
