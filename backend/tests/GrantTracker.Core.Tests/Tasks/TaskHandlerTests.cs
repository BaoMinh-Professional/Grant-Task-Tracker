using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Grants;
using GrantTracker.Core.Tasks;
using GrantTracker.Core.Tasks.AddTask;
using GrantTracker.Core.Tasks.CompleteTask;
using GrantTracker.Core.Tests.Common;
using GrantTracker.Core.Tests.Common.Time;
using Moq;

namespace GrantTracker.Core.Tests.Tasks;

public sealed class TaskHandlerTests
{
    private static readonly FakeBusinessClock Clock = new(TestGrants.Now, TestGrants.Today);

    private static AddTaskCommand Valid(Guid grantId) =>
        new(grantId, "Call applicant", TestGrants.Today.AddDays(2), TaskPriority.Low, "key-1");

    [Fact]
    public async Task AddForUnknownGrantIsNotFoundAndNothingIsAdded()
    {
        var grants = new Mock<IGrantRepository>();
        var tasks = new Mock<ITaskRepository>();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new AddTaskHandler(grants.Object, tasks.Object, Clock, new AuditContext()).Handle(Valid(Guid.NewGuid()), TestContext.Current.CancellationToken));
        tasks.Verify(x => x.Add(It.IsAny<GrantTask>()), Times.Never);
    }

    [Fact]
    public async Task AddOnOpenGrantAddsOnePendingTaskAndTargetsTheGrant()
    {
        var grant = TestGrants.Open();
        var grants = new Mock<IGrantRepository>();
        grants.Setup(x => x.FindAsync(grant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grant);
        var tasks = new Mock<ITaskRepository>();
        var audit = new AuditContext();

        var dto = await new AddTaskHandler(grants.Object, tasks.Object, Clock, audit).Handle(Valid(grant.Id), TestContext.Current.CancellationToken);

        tasks.Verify(x => x.Add(It.IsAny<GrantTask>()), Times.Once);
        Assert.Equal("Call applicant", dto.Title);
        Assert.Equal(TaskPriority.Low, dto.Priority);
        Assert.Equal(GrantTaskStatus.Pending, dto.Status);
        Assert.Equal(grant.Id, dto.GrantId);
        Assert.Equal(1, dto.Version);
        Assert.Equal("Grant", audit.EntityType);
        Assert.Equal(grant.Id, audit.EntityId);
    }

    [Fact]
    public async Task AddOnClosedGrantIsBr1AndNothingIsAdded()
    {
        var grant = TestGrants.Closed();
        var grants = new Mock<IGrantRepository>();
        grants.Setup(x => x.FindAsync(grant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grant);
        var tasks = new Mock<ITaskRepository>();
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new AddTaskHandler(grants.Object, tasks.Object, Clock, new AuditContext()).Handle(Valid(grant.Id), TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCodes.TaskOnClosedGrant, exception.Code);
        tasks.Verify(x => x.Add(It.IsAny<GrantTask>()), Times.Never);
    }

    [Fact]
    public async Task InvalidInputOnClosedGrantIsAValidationErrorFirst()
    {
        var grant = TestGrants.Closed();
        var grants = new Mock<IGrantRepository>();
        grants.Setup(x => x.FindAsync(grant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grant);
        var tasks = new Mock<ITaskRepository>();
        var command = new AddTaskCommand(grant.Id, " ", TestGrants.Today, TaskPriority.Low, "key-2");
        await Assert.ThrowsAsync<ValidationException>(() =>
            new AddTaskHandler(grants.Object, tasks.Object, Clock, new AuditContext()).Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompleteUnknownTaskIsNotFound()
    {
        var tasks = new Mock<ITaskRepository>();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CompleteTaskHandler(tasks.Object, Clock, new AuditContext()).Handle(new CompleteTaskCommand(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FirstCompleteChangesTheTaskAndRepeatIsNoChangeWithTheSameDate()
    {
        var grant = TestGrants.Open();
        var task = grant.AddTask("Verify bank details", TestGrants.Today.AddDays(-3), TaskPriority.High);
        var tasks = new Mock<ITaskRepository>();
        tasks.Setup(x => x.FindWithGrantAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        var first = new AuditContext();

        var firstResult = await new CompleteTaskHandler(tasks.Object, Clock, first).Handle(new CompleteTaskCommand(task.Id), TestContext.Current.CancellationToken);

        Assert.Equal(AuditOutcome.Changed, first.Outcome);
        Assert.Equal(GrantTaskStatus.Completed, firstResult.Status);
        Assert.Equal(2, firstResult.Version);
        Assert.Equal(TestGrants.Now, firstResult.CompletedAtUtc);

        var later = new FakeBusinessClock(TestGrants.Now.AddHours(1), TestGrants.Today);
        var repeat = new AuditContext();
        var repeatResult = await new CompleteTaskHandler(tasks.Object, later, repeat).Handle(new CompleteTaskCommand(task.Id), TestContext.Current.CancellationToken);

        Assert.Equal(AuditOutcome.NoChange, repeat.Outcome);
        Assert.Equal(TestGrants.Now, repeatResult.CompletedAtUtc);
        Assert.Equal(2, repeatResult.Version);
    }

    [Fact]
    public async Task CompletePendingTaskOnClosedGrantIsGrantClosed()
    {
        var grant = TestGrants.Closed();
        var task = grant.AddTask("Survey", TestGrants.Today.AddDays(4), TaskPriority.Low);
        var tasks = new Mock<ITaskRepository>();
        tasks.Setup(x => x.FindWithGrantAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new CompleteTaskHandler(tasks.Object, Clock, new AuditContext()).Handle(new CompleteTaskCommand(task.Id), TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCodes.GrantClosed, exception.Code);
    }

    [Fact]
    public async Task CompleteAlreadyCompletedTaskOnClosedGrantIsNoChange()
    {
        var grant = TestGrants.Closed();
        var task = grant.AddTask("Final acquittal", TestGrants.Today.AddDays(-150), TaskPriority.High, GrantTaskStatus.Completed);
        var tasks = new Mock<ITaskRepository>();
        tasks.Setup(x => x.FindWithGrantAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        var audit = new AuditContext();

        var dto = await new CompleteTaskHandler(tasks.Object, Clock, audit).Handle(new CompleteTaskCommand(task.Id), TestContext.Current.CancellationToken);

        Assert.Equal(AuditOutcome.NoChange, audit.Outcome);
        Assert.Equal(GrantTaskStatus.Completed, dto.Status);
    }
}
