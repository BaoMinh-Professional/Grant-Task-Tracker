using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Grants;
using GrantTracker.Core.Tasks;

namespace GrantTracker.Core.Tests.Grants;

public sealed class GrantTests
{
    private static readonly DateOnly Today = new(2026, 3, 10);
    private static readonly DateOnly ApplicationDate = new(2026, 2, 1);
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    private static Grant OpenGrant() =>
        new(Guid.NewGuid(), "GR-1", "Org", ApplicationDate, GrantStatus.Open, null);

    private static Grant ClosedGrant() =>
        new(Guid.NewGuid(), "GR-2", "Org", ApplicationDate, GrantStatus.Closed, Now.AddDays(-1));

    private static GrantTask AddTaskTo(Grant grant, GrantTaskStatus status, TaskPriority priority)
    {
        var completed = status == GrantTaskStatus.Completed ? Now : (DateTime?)null;
        var task = new GrantTask(Guid.NewGuid(), "Task", grant.Id, Today.AddDays(5), priority, status, completed, Now);
        grant.Tasks.Add(task);
        return task;
    }

    [Fact]
    public void AddTaskOnOpenGrantCreatesPendingTask()
    {
        var grant = OpenGrant();
        var task = grant.AddTask(Guid.NewGuid(), "Check", Today, TaskPriority.High, Today, Now);
        Assert.Equal(GrantTaskStatus.Pending, task.Status);
        Assert.Equal(1, task.Version);
        Assert.Contains(task, grant.Tasks);
    }

    [Fact]
    public void AddTaskOnClosedGrantIsRefusedWithBr1()
    {
        var grant = ClosedGrant();
        var exception = Assert.Throws<ConflictException>(() =>
            grant.AddTask(Guid.NewGuid(), "Check", Today, TaskPriority.High, Today, Now));
        Assert.Equal(ErrorCodes.TaskOnClosedGrant, exception.Code);
        Assert.Empty(grant.Tasks);
    }

    [Fact]
    public void InvalidInputOnClosedGrantReportsValidationFirst()
    {
        var grant = ClosedGrant();
        Assert.Throws<ValidationException>(() =>
            grant.AddTask(Guid.NewGuid(), " ", Today, TaskPriority.High, Today, Now));
    }

    [Fact]
    public void CloseIsRefusedWhileHighPriorityTaskIsPending()
    {
        var grant = OpenGrant();
        AddTaskTo(grant, GrantTaskStatus.Pending, TaskPriority.High);
        var exception = Assert.Throws<ConflictException>(() => grant.Close(Now));
        Assert.Equal(ErrorCodes.CloseBlockedByHighPriorityTask, exception.Code);
        Assert.Equal(GrantStatus.Open, grant.Status);
    }

    [Fact]
    public void CloseSucceedsWhenOnlyMediumAndLowTasksArePending()
    {
        var grant = OpenGrant();
        AddTaskTo(grant, GrantTaskStatus.Pending, TaskPriority.Medium);
        AddTaskTo(grant, GrantTaskStatus.Pending, TaskPriority.Low);
        Assert.True(grant.Close(Now));
        Assert.Equal(GrantStatus.Closed, grant.Status);
        Assert.Equal(Now, grant.ClosedAtUtc);
    }

    [Fact]
    public void CloseSucceedsWhenHighPriorityTaskIsCompleted()
    {
        var grant = OpenGrant();
        AddTaskTo(grant, GrantTaskStatus.Completed, TaskPriority.High);
        Assert.True(grant.Close(Now));
    }

    [Fact]
    public void CloseOnClosedGrantReturnsFalseAndKeepsClosedAt()
    {
        var grant = ClosedGrant();
        var closedAt = grant.ClosedAtUtc;
        Assert.False(grant.Close(Now));
        Assert.Equal(closedAt, grant.ClosedAtUtc);
    }

    [Fact]
    public void CompleteTaskOnClosedGrantIsRefusedWhenPending()
    {
        var grant = ClosedGrant();
        var task = AddTaskTo(grant, GrantTaskStatus.Pending, TaskPriority.Low);
        var exception = Assert.Throws<ConflictException>(() => grant.CompleteTask(task, Now));
        Assert.Equal(ErrorCodes.GrantClosed, exception.Code);
        Assert.Equal(GrantTaskStatus.Pending, task.Status);
    }

    [Fact]
    public void CompleteTaskOnClosedGrantIsNoOpWhenAlreadyCompleted()
    {
        var grant = ClosedGrant();
        var task = AddTaskTo(grant, GrantTaskStatus.Completed, TaskPriority.Low);
        Assert.False(grant.CompleteTask(task, Now.AddHours(1)));
        Assert.Equal(Now, task.CompletedAtUtc);
    }

    [Fact]
    public void CompleteTaskRejectsTaskOfAnotherGrant()
    {
        var grant = OpenGrant();
        var other = OpenGrant();
        var task = AddTaskTo(other, GrantTaskStatus.Pending, TaskPriority.Low);
        Assert.Throws<ArgumentException>(() => grant.CompleteTask(task, Now));
    }

    [Theory]
    [InlineData(GrantStatus.Open, 0, true)]
    [InlineData(GrantStatus.Open, 1, false)]
    [InlineData(GrantStatus.Closed, 0, false)]
    public void CanCloseFollowsTheRule(GrantStatus status, int pendingHighTasks, bool expected)
    {
        Assert.Equal(expected, GrantRules.CanClose(status, pendingHighTasks));
    }
}
