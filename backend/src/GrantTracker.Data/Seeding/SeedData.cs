using GrantTracker.Core.Grants;
using GrantTracker.Core.Tasks;
using GrantTracker.Core.Users;

namespace GrantTracker.Data.Seeding;

public static class SeedData
{
    private static readonly Guid[] UserIds =
    [
        Guid.Parse("00000000-0000-0000-0000-000000000001"),
        Guid.Parse("00000000-0000-0000-0000-000000000002"),
        Guid.Parse("00000000-0000-0000-0000-000000000003"),
        Guid.Parse("00000000-0000-0000-0000-000000000004"),
        Guid.Parse("00000000-0000-0000-0000-000000000005")
    ];

    public static IReadOnlyList<User> Users =>
    [
        new(UserIds[0], "Alex Nguyen", "alex.nguyen@example.org"),
        new(UserIds[1], "Priya Sharma", "priya.sharma@example.org"),
        new(UserIds[2], "Sam Taylor", "sam.taylor@example.org"),
        new(UserIds[3], "Jordan Lee", "jordan.lee@example.org"),
        new(UserIds[4], "Maria Garcia", "maria.garcia@example.org")
    ];

    public static Grant[] CreateGrants(DateOnly today, DateTime now) =>
    [
        new(Id(101), "GR-100001", "Riverside Community Garden Inc.", today.AddDays(-60), GrantStatus.Open, null),
        new(Id(102), "GR-100002", "Northside Youth Sports Club", today.AddDays(-20), GrantStatus.Open, null),
        new(Id(103), "GR-100003", "Harbour Arts Collective", today.AddDays(-200), GrantStatus.Closed, now.AddDays(-140)),
        new(Id(104), "GR-100004", "Hillview Library Friends Group", today.AddDays(-5), GrantStatus.Open, null)
    ];

    public static void AddTasks(GrantTrackerDbContext db, DateOnly today, DateTime now) =>
        db.Tasks.AddRange(
            Task(201, "Verify bank details", 101, today.AddDays(-3), TaskPriority.High, GrantTaskStatus.Pending, null, now.AddDays(-60)),
            Task(202, "Request quote for tools", 101, today.AddDays(5), TaskPriority.Medium, GrantTaskStatus.Pending, null, now.AddDays(-60)),
            Task(203, "Check eligibility documents", 101, today.AddDays(-50), TaskPriority.High, GrantTaskStatus.Completed, now.AddDays(-52), now.AddDays(-60)),
            Task(204, "Review progress report", 102, today.AddDays(-1), TaskPriority.Low, GrantTaskStatus.Pending, null, now.AddDays(-20)),
            Task(205, "Schedule site visit", 102, today.AddDays(10), TaskPriority.Medium, GrantTaskStatus.Pending, null, now.AddDays(-20)),
            Task(206, "Final acquittal review", 103, today.AddDays(-150), TaskPriority.High, GrantTaskStatus.Completed, now.AddDays(-152), now.AddDays(-200)));

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    private static GrantTask Task(int id, string title, int grant, DateOnly due, TaskPriority priority, GrantTaskStatus status, DateTime? completed, DateTime created) =>
        new(Id(id), title, Id(grant), due, priority, status, completed, created);
}
