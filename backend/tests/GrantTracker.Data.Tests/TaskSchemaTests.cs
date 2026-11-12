using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Grants;
using GrantTracker.Core.Tasks;
using GrantTracker.Data;
using GrantTracker.Data.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Data.Tests;

public sealed class TaskSchemaTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UpgradeKeepsExistingTasksAndStartsVersionAtOne()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitialiseAsync("20261111112413_InitialCreate");
        var grantId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var title = new string('t', 50);
        await using (var before = database.CreateContext())
        {
            await before.Database.ExecuteSqlAsync($"INSERT INTO Grants (Id, ReferenceNumber, OrganisationName, ApplicationDate, Status, ClosedAtUtc) VALUES ({grantId}, {"GR-9"}, {"Org"}, {"2026-01-01"}, {"Open"}, {null})");
            await before.Database.ExecuteSqlAsync($"INSERT INTO Tasks (Id, Title, GrantId, DueDate, Priority, Status, CompletedAtUtc, CreatedAtUtc) VALUES ({taskId}, {title}, {grantId}, {"2026-03-07"}, {"High"}, {"Pending"}, {null}, {Now})");
        }

        await using var upgraded = database.CreateContext();
        await upgraded.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var task = await upgraded.Tasks.AsNoTracking().SingleAsync(t => t.Id == taskId, TestContext.Current.CancellationToken);
        Assert.Equal(title, task.Title);
        Assert.Equal(1, task.Version);
    }

    [Fact]
    public async Task HundredCharacterTitleIsAcceptedAndOneHundredAndOneIsRejected()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitialiseAsync();
        await using var context = database.CreateContext();
        var grant = new Grant(Guid.NewGuid(), "GR-8", "Org", new DateOnly(2026, 1, 1), GrantStatus.Open, null);
        context.Grants.Add(grant);
        var hundred = new GrantTask(Guid.NewGuid(), new string('a', 100), grant.Id, new DateOnly(2026, 3, 10),
            TaskPriority.Low, GrantTaskStatus.Pending, null, Now);
        context.Tasks.Add(hundred);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlAsync(
            $"INSERT INTO Tasks (Id, Title, GrantId, DueDate, Priority, Status, CompletedAtUtc, CreatedAtUtc) VALUES ({Guid.NewGuid()}, {new string('a', 101)}, {grant.Id}, {"2026-03-10"}, {"Low"}, {"Pending"}, {null}, {Now})",
            TestContext.Current.CancellationToken));
        Assert.Equal(275, exception.SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task StaleVersionIsRejectedByTheDatabase()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitialiseAsync();
        var grant = new Grant(Guid.NewGuid(), "GR-7", "Org", new DateOnly(2026, 1, 1), GrantStatus.Open, null);
        var task = new GrantTask(Guid.NewGuid(), "Race", grant.Id, new DateOnly(2026, 3, 10), TaskPriority.Low,
            GrantTaskStatus.Pending, null, Now);
        await using (var seed = database.CreateContext())
        {
            seed.Grants.Add(grant);
            seed.Tasks.Add(task);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var firstTask = await first.Tasks.Include(t => t.Grant).SingleAsync(t => t.Id == task.Id, TestContext.Current.CancellationToken);
        var secondTask = await second.Tasks.Include(t => t.Grant).SingleAsync(t => t.Id == task.Id, TestContext.Current.CancellationToken);
        firstTask.Grant!.CompleteTask(firstTask, Now);
        secondTask.Grant!.CompleteTask(secondTask, Now.AddMinutes(1));
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RaceThroughTheUnitOfWorkIsAVersionConflict()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitialiseAsync();
        var grant = new Grant(Guid.NewGuid(), "GR-6", "Org", new DateOnly(2026, 1, 1), GrantStatus.Open, null);
        var task = new GrantTask(Guid.NewGuid(), "Race", grant.Id, new DateOnly(2026, 3, 10), TaskPriority.Low,
            GrantTaskStatus.Pending, null, Now);
        await using (var seed = database.CreateContext())
        {
            seed.Grants.Add(grant);
            seed.Tasks.Add(task);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var firstTask = await first.Tasks.Include(t => t.Grant).SingleAsync(t => t.Id == task.Id, TestContext.Current.CancellationToken);
        var secondTask = await second.Tasks.Include(t => t.Grant).SingleAsync(t => t.Id == task.Id, TestContext.Current.CancellationToken);
        firstTask.Grant!.CompleteTask(firstTask, Now);
        secondTask.Grant!.CompleteTask(secondTask, Now.AddMinutes(1));
        await new EfUnitOfWork(first).SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<VersionConflictException>(() =>
            new EfUnitOfWork(second).SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
