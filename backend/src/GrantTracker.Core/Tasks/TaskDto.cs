namespace GrantTracker.Core.Tasks;

public sealed record TaskDto(Guid Id, Guid GrantId, string Title, TaskPriority Priority, DateOnly DueDate,
    GrantTaskStatus Status, DateTime? CompletedAtUtc, bool IsOverdue, int DaysOverdue, int Version)
{
    public static TaskDto From(GrantTask t, DateOnly today) => new(t.Id, t.GrantId, t.Title, t.Priority,
        t.DueDate, t.Status, t.CompletedAtUtc, t.IsOverdue(today), t.DaysOverdue(today), t.Version);
}
