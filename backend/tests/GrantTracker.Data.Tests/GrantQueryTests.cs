using GrantTracker.Core.Grants;
using GrantTracker.Core.Tasks;
using GrantTracker.Data.Repositories;
using GrantTracker.Data.Seeding;
using GrantTracker.Data.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrantTracker.Data.Tests;

public sealed class GrantQueryTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 3, 10);
    private static readonly Guid Grant1 = Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid Grant3 = Guid.Parse("00000000-0000-0000-0000-000000000103");

    private static async Task<SqliteTestDatabase> SeededDatabaseAsync(CancellationToken cancellationToken)
    {
        var database = new SqliteTestDatabase();
        await database.InitialiseAsync();
        await using var context = database.CreateContext();
        var seeder = new DbSeeder(context, new FakeBusinessClock(Now, Today), NullLogger<DbSeeder>.Instance);
        await seeder.SeedAsync(cancellationToken);
        return database;
    }

    [Fact]
    public async Task ListOrdersOpenGrantsFirstByApplicationDate()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = await SeededDatabaseAsync(token);
        await using var context = database.CreateContext();
        var summaries = await new GrantRepository(context).ListAsync(null, token);
        Assert.Equal(["GR-100001", "GR-100002", "GR-100004", "GR-100003"], summaries.Select(s => s.ReferenceNumber));
    }

    [Fact]
    public async Task ListFiltersByStatus()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = await SeededDatabaseAsync(token);
        await using var context = database.CreateContext();
        var repository = new GrantRepository(context);
        Assert.Equal(3, (await repository.ListAsync(GrantStatus.Open, token)).Count);
        Assert.Equal(1, (await repository.ListAsync(GrantStatus.Closed, token)).Count);
    }

    [Fact]
    public async Task ListReturnsTaskCountsAndPendingHighCount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = await SeededDatabaseAsync(token);
        await using var context = database.CreateContext();
        var summaries = (await new GrantRepository(context).ListAsync(null, token)).ToDictionary(s => s.ReferenceNumber);
        Assert.Equal((3, 1, 1), (summaries["GR-100001"].TotalTasks, summaries["GR-100001"].CompletedTasks, summaries["GR-100001"].PendingHighTasks));
        Assert.Equal((2, 0, 0), (summaries["GR-100002"].TotalTasks, summaries["GR-100002"].CompletedTasks, summaries["GR-100002"].PendingHighTasks));
        Assert.Equal((1, 1, 0), (summaries["GR-100003"].TotalTasks, summaries["GR-100003"].CompletedTasks, summaries["GR-100003"].PendingHighTasks));
        Assert.Equal((0, 0, 0), (summaries["GR-100004"].TotalTasks, summaries["GR-100004"].CompletedTasks, summaries["GR-100004"].PendingHighTasks));
    }

    [Fact]
    public async Task FindWithTasksLoadsTheGrantsTasks()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = await SeededDatabaseAsync(token);
        await using var context = database.CreateContext();
        var repository = new GrantRepository(context);
        var grant = await repository.FindWithTasksAsync(Grant1, token);
        Assert.Equal(3, grant!.Tasks.Count);
        Assert.Null(await repository.FindWithTasksAsync(Guid.NewGuid(), token));
    }

    [Fact]
    public async Task AddedTaskIsInsertedAndStartsAtVersionOne()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = await SeededDatabaseAsync(token);
        var taskId = Guid.NewGuid();
        await using (var context = database.CreateContext())
        {
            var grant = await new GrantRepository(context).FindAsync(Grant1, token);
            var task = grant!.AddTask(taskId, "Call applicant", Today.AddDays(2), TaskPriority.Low, Today, Now);
            new TaskRepository(context).Add(task);
            await context.SaveChangesAsync(token);
        }

        await using var reader = database.CreateContext();
        var saved = await reader.Tasks.AsNoTracking().SingleAsync(t => t.Id == taskId, token);
        Assert.Equal(1, saved.Version);
        Assert.Equal(GrantTaskStatus.Pending, saved.Status);
        Assert.Equal(4, await reader.Tasks.CountAsync(t => t.GrantId == Grant1, token));
    }

    [Fact]
    public async Task OverdueListIsOrderedAndExcludesCompletedTasks()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = await SeededDatabaseAsync(token);
        await using var context = database.CreateContext();
        var overdue = await new TaskRepository(context).ListOverdueAsync(Today, token);
        Assert.Equal(["Verify bank details", "Review progress report"], overdue.Select(t => t.Title));
        Assert.Equal(new DateOnly(2026, 3, 7), overdue[0].DueDate);
    }

    [Fact]
    public async Task OverdueListIncludesPendingTasksOfClosedGrants()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = await SeededDatabaseAsync(token);
        await using (var context = database.CreateContext())
        {
            context.Tasks.Add(new GrantTask(Guid.NewGuid(), "Late on closed grant", Grant3, Today.AddDays(-1),
                TaskPriority.Low, GrantTaskStatus.Pending, null, Now));
            await context.SaveChangesAsync(token);
        }

        await using var reader = database.CreateContext();
        var overdue = await new TaskRepository(reader).ListOverdueAsync(Today, token);
        var closed = Assert.Single(overdue, t => t.Title == "Late on closed grant");
        Assert.Equal(GrantStatus.Closed, closed.Grant!.Status);
    }

    [Fact]
    public async Task FindWithGrantLoadsTheGrant()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = await SeededDatabaseAsync(token);
        await using var context = database.CreateContext();
        var repository = new TaskRepository(context);
        var task = await context.Tasks.AsNoTracking().FirstAsync(t => t.Title == "Verify bank details", token);
        var loaded = await repository.FindWithGrantAsync(task.Id, token);
        Assert.Equal("GR-100001", loaded!.Grant!.ReferenceNumber);
        Assert.Null(await repository.FindWithGrantAsync(Guid.NewGuid(), token));
    }
}
