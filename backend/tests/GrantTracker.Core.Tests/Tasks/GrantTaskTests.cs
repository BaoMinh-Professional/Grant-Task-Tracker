using GrantTracker.Core.Tasks;

namespace GrantTracker.Core.Tests.Tasks;

public sealed class GrantTaskTests
{
    private static readonly DateOnly Today = new(2026, 3, 10);
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    private static GrantTask PendingTask(DateOnly dueDate) =>
        new(Guid.NewGuid(), "Task", Guid.NewGuid(), dueDate, TaskPriority.Medium, GrantTaskStatus.Pending, null, Now);

    [Fact]
    public void CompleteSetsStatusAndIncrementsVersionOnlyOnce()
    {
        var task = PendingTask(Today);
        Assert.Equal(1, task.Version);

        Assert.True(task.Complete(Now));
        Assert.Equal(GrantTaskStatus.Completed, task.Status);
        Assert.Equal(Now, task.CompletedAtUtc);
        Assert.Equal(2, task.Version);

        Assert.False(task.Complete(Now.AddHours(1)));
        Assert.Equal(Now, task.CompletedAtUtc);
        Assert.Equal(2, task.Version);
    }

    [Fact]
    public void PendingTaskDueBeforeTodayIsOverdue()
    {
        var task = PendingTask(Today.AddDays(-1));
        Assert.True(task.IsOverdue(Today));
        Assert.Equal(1, task.DaysOverdue(Today));
    }

    [Fact]
    public void PendingTaskDueTodayIsNotOverdue()
    {
        var task = PendingTask(Today);
        Assert.False(task.IsOverdue(Today));
        Assert.Equal(0, task.DaysOverdue(Today));
    }

    [Fact]
    public void PendingTaskThreeDaysLateReportsThreeDays()
    {
        var task = PendingTask(Today.AddDays(-3));
        Assert.Equal(3, task.DaysOverdue(Today));
    }

    [Fact]
    public void CompletedTaskIsNeverOverdue()
    {
        var task = new GrantTask(Guid.NewGuid(), "Task", Guid.NewGuid(), Today.AddDays(-3), TaskPriority.High,
            GrantTaskStatus.Completed, Now.AddDays(-2), Now);
        Assert.False(task.IsOverdue(Today));
        Assert.Equal(0, task.DaysOverdue(Today));
    }
}
