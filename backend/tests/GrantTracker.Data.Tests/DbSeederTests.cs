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
        await using var database = new SqliteTestDatabase();
        await database.InitialiseAsync();
        await using var context = database.CreateContext();
        var clock = new FakeBusinessClock(new DateTime(2026, 3, 10, 9, 0, 0), new DateOnly(2026, 3, 10));
        var seeder = new DbSeeder(context, clock, NullLogger<DbSeeder>.Instance);
        await seeder.SeedAsync();
        context.ChangeTracker.Clear();
        Assert.Equal(5, await context.Users.CountAsync());
        Assert.Equal(5, await context.Grants.CountAsync());
        Assert.Equal(6, await context.GrantAssignments.CountAsync());
        Assert.Equal(8, await context.TaskAssignments.CountAsync());
        Assert.Equal(new DateOnly(2026, 2, 28), await context.Tasks.Where(x => x.Title == "Verify bank details").Select(x => x.DueDate).SingleAsync());
        await seeder.SeedAsync();
        Assert.Equal(9, await context.Tasks.CountAsync());
    }
}
