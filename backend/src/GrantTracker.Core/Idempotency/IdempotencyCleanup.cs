using GrantTracker.Core.Common.Time;

namespace GrantTracker.Core.Idempotency;

public sealed class IdempotencyCleanup(IIdempotencyStore store, IBusinessClock clock)
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default) =>
        store.DeleteCreatedBeforeAsync(clock.UtcNow - Retention, cancellationToken);
}
