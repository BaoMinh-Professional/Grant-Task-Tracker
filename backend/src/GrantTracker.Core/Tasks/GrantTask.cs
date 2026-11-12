using GrantTracker.Core.Assignments;
using GrantTracker.Core.Grants;

namespace GrantTracker.Core.Tasks;

public sealed class GrantTask
{
    private GrantTask() { }
    public GrantTask(Guid id, string title, Guid grantId, DateOnly dueDate, TaskPriority priority,
        GrantTaskStatus status, DateTime? completedAtUtc, DateTime createdAtUtc)
    {
        if ((status == GrantTaskStatus.Completed) != completedAtUtc.HasValue) throw new ArgumentException("Status and completion timestamp must be consistent.");
        Id = id; Title = title; GrantId = grantId; DueDate = dueDate; Priority = priority;
        Status = status; CompletedAtUtc = completedAtUtc; CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = "";
    public Guid GrantId { get; private set; }
    public Grant? Grant { get; private set; }
    public DateOnly DueDate { get; private set; }
    public TaskPriority Priority { get; private set; }
    public GrantTaskStatus Status { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int Version { get; private set; } = 1;
    public ICollection<TaskAssignment> Assignments { get; private set; } = new List<TaskAssignment>();

    public bool Complete(DateTime completedAtUtc)
    {
        if (Status == GrantTaskStatus.Completed) return false;
        Status = GrantTaskStatus.Completed;
        CompletedAtUtc = completedAtUtc;
        Version++;
        return true;
    }

    public bool IsOverdue(DateOnly today) => Status == GrantTaskStatus.Pending && DueDate < today;

    public int DaysOverdue(DateOnly today) => IsOverdue(today) ? today.DayNumber - DueDate.DayNumber : 0;

    public void Edit(ValidatedTask valid)
    {
        Title = valid.Title;
        DueDate = valid.DueDate;
        Priority = valid.Priority;
        Version++;
    }
}
