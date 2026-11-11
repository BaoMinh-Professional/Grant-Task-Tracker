using GrantTracker.Core.Assignments;
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
        new(Id(102), "GR-100002", "Northside Youth Sports Club", today.AddDays(-30), GrantStatus.Open, null),
        new(Id(103), "GR-100003", "Harbour Seniors Network", today.AddDays(-14), GrantStatus.Open, null),
        new(Id(104), "GR-100004", "Eastside Arts Collective", today.AddDays(-120), GrantStatus.Closed, now.AddDays(-25)),
        new(Id(105), "GR-100005", "Westfield Food Bank", today.AddDays(-5), GrantStatus.Open, null)
    ];

    public static void AddTasks(GrantTrackerDbContext db, DateOnly today, DateTime now)
    {
        var tasks = new[]
        {
            Task(201, "Verify bank details", 101, today.AddDays(-10), TaskPriority.High, GrantTaskStatus.Pending, null, now),
            Task(202, "Review budget", 101, today.AddDays(-3), TaskPriority.Medium, GrantTaskStatus.Pending, null, now),
            Task(203, "Send acknowledgement letter", 101, today.AddDays(-20), TaskPriority.Low, GrantTaskStatus.Completed, now.AddDays(-18), now),
            Task(204, "Schedule site visit", 102, today, TaskPriority.Medium, GrantTaskStatus.Pending, null, now),
            Task(205, "Check eligibility", 102, today.AddDays(-1), TaskPriority.Low, GrantTaskStatus.Pending, null, now),
            Task(206, "Collect insurance certificate", 102, today.AddDays(7), TaskPriority.High, GrantTaskStatus.Pending, null, now),
            Task(207, "Initial eligibility check", 103, today.AddDays(-7), TaskPriority.High, GrantTaskStatus.Completed, now.AddDays(-8), now),
            Task(208, "Confirm contact details", 103, today.AddDays(14), TaskPriority.Low, GrantTaskStatus.Pending, null, now),
            Task(209, "Final report received", 104, today.AddDays(-30), TaskPriority.Medium, GrantTaskStatus.Completed, now.AddDays(-32), now)
        };
        db.Tasks.AddRange(tasks);
    }

    public static GrantAssignment[] CreateGrantAssignments() =>
    [
            G(301, 1, 101), G(302, 2, 101), G(303, 3, 102), G(304, 1, 102), G(305, 4, 103), G(306, 5, 104)
    ];

    public static TaskAssignment[] CreateTaskAssignments() =>
    [
            T(401, 1, 201), T(402, 2, 202), T(403, 1, 203), T(404, 3, 204),
            T(405, 3, 205), T(406, 1, 206), T(407, 4, 207), T(408, 5, 209)
    ];

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    private static GrantTask Task(int id, string title, int grant, DateOnly due, TaskPriority priority, GrantTaskStatus status, DateTime? completed, DateTime created) =>
        new(Id(id), title, Id(grant), due, priority, status, completed, created);
    private static GrantAssignment G(int id, int user, int grant) => new(Id(id), UserIds[user - 1], Id(grant));
    private static TaskAssignment T(int id, int user, int task) => new(Id(id), UserIds[user - 1], Id(task));
}
