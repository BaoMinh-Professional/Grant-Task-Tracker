namespace GrantTracker.Core.Assignments;

public sealed class GrantAssignment
{
    private GrantAssignment() { }
    public GrantAssignment(Guid id, Guid userId, Guid grantId) { Id = id; UserId = userId; GrantId = grantId; }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GrantId { get; private set; }
}
