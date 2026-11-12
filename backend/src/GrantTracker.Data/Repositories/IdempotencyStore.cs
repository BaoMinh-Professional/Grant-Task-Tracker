using GrantTracker.Core.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Data.Repositories;

public sealed class IdempotencyStore(GrantTrackerDbContext dbContext) : IIdempotencyStore
{
    public Task<IdempotencyRecord?> FindAsync(string key, CancellationToken cancellationToken = default) =>
        dbContext.IdempotencyRecords.SingleOrDefaultAsync(record => record.Key == key, cancellationToken);

    public void Add(IdempotencyRecord record) => dbContext.IdempotencyRecords.Add(record);

    public Task<int> DeleteCreatedBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
        dbContext.IdempotencyRecords.Where(record => record.CreatedAtUtc < cutoffUtc).ExecuteDeleteAsync(cancellationToken);
}
