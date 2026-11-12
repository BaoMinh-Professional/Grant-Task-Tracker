using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Grants;
using GrantTracker.Core.Tasks;
using GrantTracker.Core.Tasks.EditTask;
using GrantTracker.Core.Tests.Common;
using GrantTracker.Core.Tests.Common.Time;
using Moq;

namespace GrantTracker.Core.Tests.Tasks;

public sealed class EditTaskHandlerTests
{
    private static readonly FakeBusinessClock Clock = new(TestGrants.Now, TestGrants.Today);
    private static readonly DateOnly PastDue = new(2026, 3, 7);

    private static EditTaskHandler HandlerFor(GrantTask task)
    {
        var tasks = new Mock<ITaskRepository>();
        tasks.Setup(x => x.FindWithGrantAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        return new EditTaskHandler(tasks.Object, Clock, new AuditContext());
    }

    private static GrantTask PendingTask(Grant grant, DateOnly dueDate) =>
        grant.AddTask("Verify bank details", dueDate, TaskPriority.High);

    [Fact]
    public async Task StaleVersionIsAVersionConflict()
    {
        var task = PendingTask(TestGrants.Open(), TestGrants.Today.AddDays(5));
        var handler = HandlerFor(task);

        var exception = await Assert.ThrowsAsync<VersionConflictException>(() =>
            handler.Handle(new EditTaskCommand(task.Id, 0, "Renamed", TestGrants.Today.AddDays(5), TaskPriority.High, "k1"),
                TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.VersionConflict, exception.Code);
    }

    [Fact]
    public async Task MissingVersionIsPreconditionRequired()
    {
        var task = PendingTask(TestGrants.Open(), TestGrants.Today.AddDays(5));
        var handler = HandlerFor(task);

        await Assert.ThrowsAsync<PreconditionRequiredException>(() =>
            handler.Handle(new EditTaskCommand(task.Id, null, "Renamed", TestGrants.Today.AddDays(5), TaskPriority.High, "k2"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnchangedPastDueDateIsAcceptedAndBumpsTheVersion()
    {
        var task = PendingTask(TestGrants.Open(), PastDue);
        var handler = HandlerFor(task);

        var dto = await handler.Handle(new EditTaskCommand(task.Id, 1, "Renamed", PastDue, TaskPriority.Medium, "k3"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Renamed", dto.Title);
        Assert.Equal(TaskPriority.Medium, dto.Priority);
        Assert.Equal(PastDue, dto.DueDate);
        Assert.Equal(2, dto.Version);
    }

    [Fact]
    public async Task ChangingTheDueDateToThePastIsBr2()
    {
        var task = PendingTask(TestGrants.Open(), TestGrants.Today.AddDays(5));
        var handler = HandlerFor(task);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new EditTaskCommand(task.Id, 1, "Renamed", new DateOnly(2026, 3, 1), TaskPriority.High, "k4"),
                TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.DueDateInvalid, exception.Code);
    }

    [Fact]
    public async Task CompletedTaskCannotBeEdited()
    {
        var grant = TestGrants.Open();
        var task = grant.AddTask("Final report", TestGrants.Today.AddDays(-2), TaskPriority.Low, GrantTaskStatus.Completed);
        var handler = HandlerFor(task);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new EditTaskCommand(task.Id, 1, "Renamed", TestGrants.Today.AddDays(-2), TaskPriority.Low, "k5"),
                TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.TaskCompleted, exception.Code);
    }

    [Fact]
    public async Task PendingTaskOnClosedGrantIsGrantClosed()
    {
        var grant = TestGrants.Closed();
        var task = grant.AddTask("Survey", TestGrants.Today.AddDays(4), TaskPriority.Low);
        var handler = HandlerFor(task);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new EditTaskCommand(task.Id, 1, "Renamed", TestGrants.Today.AddDays(4), TaskPriority.Low, "k6"),
                TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.GrantClosed, exception.Code);
    }
}
