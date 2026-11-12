namespace GrantTracker.Core.Idempotency;

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(string key, CancellationToken cancellationToken = default);
    void Add(IdempotencyRecord record);
    Task<int> DeleteCreatedBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
