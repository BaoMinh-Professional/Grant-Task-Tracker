namespace GrantTracker.Core.Auditing;

public sealed class AuditContext : IAuditContext
{
    public AuditOutcome Outcome { get; private set; } = AuditOutcome.Changed;
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public void SetTarget(string? entityType, Guid? entityId) { EntityType = entityType; EntityId = entityId; }
    public void SetOutcome(AuditOutcome outcome) => Outcome = outcome;
}
