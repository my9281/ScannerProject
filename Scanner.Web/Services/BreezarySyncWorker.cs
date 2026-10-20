using Microsoft.Extensions.Options;

namespace Scanner.Web.Services;

public sealed class BreezarySyncWorker(IServiceScopeFactory scopes, IOptions<TencentDocsOptions> options,
    ILogger<BreezarySyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.SyncEnabled) return;
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(options.Value.SyncIntervalSeconds, 30, 86400)));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sync = scope.ServiceProvider.GetRequiredService<BreezarySyncService>();
                if (options.Value.SyncAutoPush)
                {
                    var result = await sync.PushAsync(stoppingToken);
                    if (result.Conflicts + result.Failed > 0) logger.LogWarning("Breezary sync: {Conflicts} conflicts; {Failed} failed pushes", result.Conflicts, result.Failed);
                }
                await sync.PullAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (ApiException ex) { logger.LogWarning("Breezary background sync: {Reason}", ex.Message); }
            catch (Exception ex) { logger.LogError(ex, "Breezary background sync failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
