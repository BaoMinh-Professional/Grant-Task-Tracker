using GrantTracker.Core.Assignments;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Tasks;

namespace GrantTracker.Core.Grants;

public sealed class Grant
{
    private Grant() { }
    public Grant(Guid id, string referenceNumber, string organisationName, DateOnly applicationDate, GrantStatus status, DateTime? closedAtUtc)
    {
        if ((status == GrantStatus.Closed) != closedAtUtc.HasValue) throw new ArgumentException("Status and closed timestamp must be consistent.");
        Id = id; ReferenceNumber = referenceNumber; OrganisationName = organisationName;
        ApplicationDate = applicationDate; Status = status; ClosedAtUtc = closedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ReferenceNumber { get; private set; } = "";
    public string OrganisationName { get; private set; } = "";
    public DateOnly ApplicationDate { get; private set; }
    public GrantStatus Status { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public ICollection<GrantTask> Tasks { get; private set; } = new List<GrantTask>();
    public ICollection<GrantAssignment> Assignments { get; private set; } = new List<GrantAssignment>();

    public int PendingHighTaskCount => Tasks.Count(t =>
        t.Status == GrantTaskStatus.Pending && t.Priority == TaskPriority.High);

    public bool CanClose => GrantRules.CanClose(Status, PendingHighTaskCount);

    public GrantTask AddTask(Guid taskId, string? title, DateOnly? dueDate, TaskPriority? priority,
        DateOnly today, DateTime nowUtc)
    {
        var valid = TaskRules.Validate(title, dueDate, priority, ApplicationDate, today);
        if (Status == GrantStatus.Closed)
            throw new ConflictException(ErrorCodes.TaskOnClosedGrant, "This grant is closed, so tasks can't be added.");
        var task = new GrantTask(taskId, valid.Title, Id, valid.DueDate, valid.Priority,
            GrantTaskStatus.Pending, null, nowUtc);
        Tasks.Add(task);
        return task;
    }

    public bool CompleteTask(GrantTask task, DateTime nowUtc)
    {
        if (task.GrantId != Id) throw new ArgumentException("The task belongs to another grant.", nameof(task));
        if (task.Status == GrantTaskStatus.Completed) return false;
        if (Status == GrantStatus.Closed)
            throw new ConflictException(ErrorCodes.GrantClosed, "This grant is closed.");
        return task.Complete(nowUtc);
    }

    public bool Close(DateTime nowUtc)
    {
        if (Status == GrantStatus.Closed) return false;
        if (PendingHighTaskCount > 0)
            throw new ConflictException(ErrorCodes.CloseBlockedByHighPriorityTask,
                "This grant can't be closed while it has High priority tasks pending.");
        Status = GrantStatus.Closed;
        ClosedAtUtc = nowUtc;
        return true;
    }

    public void EditTask(GrantTask task, string? title, DateOnly? dueDate, TaskPriority? priority, DateOnly today)
    {
        if (task.GrantId != Id) throw new ArgumentException("The task belongs to another grant.", nameof(task));
        if (Status == GrantStatus.Closed)
            throw new ConflictException(ErrorCodes.GrantClosed, "This grant is closed.");
        if (task.Status == GrantTaskStatus.Completed)
            throw new ConflictException(ErrorCodes.TaskCompleted, "A completed task can't be edited.");
        var valid = TaskRules.Validate(title, dueDate, priority, ApplicationDate, today,
            enforceDueDate: dueDate != task.DueDate);
        task.Edit(valid);
    }
}
