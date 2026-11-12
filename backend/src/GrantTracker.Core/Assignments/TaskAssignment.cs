namespace GrantTracker.Core.Assignments;

// Present but not used by the grant and task features (assignment is out of scope).
public sealed class TaskAssignment
{
    private TaskAssignment() { }
    public TaskAssignment(Guid id, Guid userId, Guid taskId) { Id = id; UserId = userId; TaskId = taskId; }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TaskId { get; private set; }
}
