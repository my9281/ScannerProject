using Microsoft.Extensions.Caching.Memory;
using Scanner.Server.DAL;

namespace Scanner.Web.Services;

public sealed record HomeMetrics(long? Routes, long? Users, long? Pallets, long? Records, long? Repairs);

public sealed class HomeMetricsService(IMySqlConnectionFactory database, IUploadService uploads, IMemoryCache cache, ILogger<HomeMetricsService> logger)
{
    public async Task<HomeMetrics> GetAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue<HomeMetrics>("home-metrics", out var cached)) return cached!;
        long? routes = null, users = null, pallets = null, records = null, repairs = null;
        try { routes = uploads.List().Count; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogWarning("Home file count unavailable."); }
        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                  (SELECT COUNT(*) FROM app_users),
                  (SELECT COUNT(*) FROM (SELECT shelving_date, pallet_number FROM shelved_pallet_data WHERE pallet_number IS NOT NULL AND TRIM(pallet_number) <> '' GROUP BY shelving_date, pallet_number) p),
                  (SELECT COUNT(*) FROM shelved_pallet_data),
                  (SELECT COUNT(*) FROM (SELECT scan_date, pallet_number FROM tester_pallet_scans GROUP BY scan_date, pallet_number) p);
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                users = reader.GetInt64(0); pallets = reader.GetInt64(1);
                records = reader.GetInt64(2); repairs = reader.GetInt64(3);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { logger.LogWarning("Home aggregate counts unavailable."); }
        var result = new HomeMetrics(routes, users, pallets, records, repairs);
        cache.Set("home-metrics", result, TimeSpan.FromSeconds(60));
        return result;
    }
}
