using GrantTracker.Core.Grants;
using GrantTracker.Core.Tasks;
using GrantTracker.Data.Seeding;
using GrantTracker.Data.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrantTracker.Data.Tests;

public sealed class DbSeederTests
{
    [Fact]
    public async Task SeedsFixedScenarioAndSkipsOnSecondRun()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqliteTestDatabase();
        await database.InitialiseAsync();
        await using var context = database.CreateContext();
        var clock = new FakeBusinessClock(new DateTime(2026, 3, 10, 9, 0, 0), new DateOnly(2026, 3, 10));
        var seeder = new DbSeeder(context, clock, NullLogger<DbSeeder>.Instance);
        await seeder.SeedAsync(token);
        context.ChangeTracker.Clear();
        Assert.Equal(5, await context.Users.CountAsync(token));
        Assert.Equal(4, await context.Grants.CountAsync(token));
        Assert.Equal(6, await context.Tasks.CountAsync(token));
        Assert.Equal(0, await context.GrantAssignments.CountAsync(token));
        Assert.Equal(0, await context.TaskAssignments.CountAsync(token));
        Assert.Equal(new DateOnly(2026, 3, 7), await context.Tasks.Where(x => x.Title == "Verify bank details").Select(x => x.DueDate).SingleAsync(token));
        Assert.Equal(new DateOnly(2026, 3, 9), await context.Tasks.Where(x => x.Title == "Review progress report").Select(x => x.DueDate).SingleAsync(token));
        Assert.Equal(GrantStatus.Closed, await context.Grants.Where(x => x.ReferenceNumber == "GR-100003").Select(x => x.Status).SingleAsync(token));
        Assert.False(await context.Tasks.AnyAsync(x => x.GrantId == context.Grants.Single(g => g.ReferenceNumber == "GR-100004").Id, token));
        Assert.Equal(1, await context.Tasks.CountAsync(x => x.Grant!.ReferenceNumber == "GR-100001" && x.Status == GrantTaskStatus.Pending && x.Priority == TaskPriority.High, token));
        await seeder.SeedAsync(token);
        Assert.Equal(6, await context.Tasks.CountAsync(token));
    }
}

