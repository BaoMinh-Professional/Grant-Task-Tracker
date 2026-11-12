using GrantTracker.Core.Idempotency;

namespace GrantTracker.Api.Idempotency;

public sealed class IdempotencyCleanupService(IServiceScopeFactory scopes, ILogger<IdempotencyCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var purged = await scope.ServiceProvider.GetRequiredService<IdempotencyCleanup>().PurgeExpiredAsync(stoppingToken);
                if (purged > 0) logger.LogInformation("Purged {Count} expired idempotency records.", purged);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Purging expired idempotency records failed; the next run will try again.");
            }
        }
    }
}
