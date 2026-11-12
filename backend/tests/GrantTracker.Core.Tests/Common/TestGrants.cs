using GrantTracker.Core.Grants;
using GrantTracker.Core.Tasks;

namespace GrantTracker.Core.Tests.Common;

internal static class TestGrants
{
    public static readonly DateOnly Today = new(2026, 3, 10);
    public static readonly DateOnly ApplicationDate = new(2026, 2, 1);
    public static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    public static Grant Open(string reference = "GR-1") =>
        new(Guid.NewGuid(), reference, "Riverside Community Garden Inc.", ApplicationDate, GrantStatus.Open, null);

    public static Grant Closed() =>
        new(Guid.NewGuid(), "GR-2", "Northside Youth Sports Club", ApplicationDate, GrantStatus.Closed, Now.AddDays(-1));

    public static GrantTask AddTask(this Grant grant, string title, DateOnly dueDate, TaskPriority priority,
        GrantTaskStatus status = GrantTaskStatus.Pending)
    {
        var task = new GrantTask(Guid.NewGuid(), title, grant.Id, dueDate, priority, status,
            status == GrantTaskStatus.Completed ? Now : null, Now);
        grant.Tasks.Add(task);
        // EF fills the Grant navigation when loading; unit tests set it by reflection because the setter is private.
        typeof(GrantTask).GetProperty(nameof(GrantTask.Grant))!.SetValue(task, grant);
        return task;
    }
}
