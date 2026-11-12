using GrantTracker.Core.Idempotency;
using GrantTracker.Data.Repositories;
using GrantTracker.Data.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Data.Tests;

public sealed class IdempotencyStoreTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DeletesOnlyRecordsCreatedBeforeTheCutoff()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitialiseAsync();
        await using var context = database.CreateContext();
        context.IdempotencyRecords.AddRange(
            new IdempotencyRecord("old", "AddTask", "hash", "{}", Now.AddHours(-25)),
            new IdempotencyRecord("recent", "AddTask", "hash", "{}", Now.AddHours(-1)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var deleted = await new IdempotencyStore(context).DeleteCreatedBeforeAsync(Now.AddHours(-24), TestContext.Current.CancellationToken);

        Assert.Equal(1, deleted);
        await using var check = database.CreateContext();
        var remaining = await check.IdempotencyRecords.Select(record => record.Key).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal("recent", Assert.Single(remaining));
    }
}
