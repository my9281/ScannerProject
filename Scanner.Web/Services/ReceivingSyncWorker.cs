using Scanner.Server.Model;

namespace Scanner.Web.Services;

public sealed class ReceivingSyncWorker(IServiceScopeFactory scopes, DatabaseOptions database, ILogger<ReceivingSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            if (!database.Enabled) continue;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<ReceivingSyncStore>();
                foreach (int domain in await store.Domains(stoppingToken))
                {
                    await store.Sync(domain, scope.ServiceProvider.GetRequiredService<BluettiReceivingClient>(), stoppingToken);
                    await scope.ServiceProvider.GetRequiredService<ReceivingTrackingScheduler>().Run(domain, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Receiving sync unavailable; verify database migrations 022/023 and database connectivity."); await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
