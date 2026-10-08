using Scanner.Server.Model;
namespace Scanner.Server.DAL;
public sealed class LocationRepository(IMySqlConnectionFactory factory) : ILocationRepository
{
    public async Task SaveAsync(IReadOnlyList<LocationScanItem> items, CancellationToken cancellationToken = default)
    {
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var item in items.OrderBy(x => x.LocationId, StringComparer.Ordinal))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO warehouse_locations (location_id, scanned_at) VALUES (@id, @time) ON DUPLICATE KEY UPDATE location_id = location_id;";
            command.Parameters.AddWithValue("@id", item.LocationId);
            command.Parameters.AddWithValue("@time", item.ScannedAt.UtcDateTime);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<LocationRow>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT location_id, is_disabled, scanned_at FROM warehouse_locations ORDER BY location_id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<LocationRow>();
        while (await reader.ReadAsync(cancellationToken)) rows.Add(new(reader.GetString(0), reader.GetBoolean(1), DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc)));
        return rows;
    }
}

