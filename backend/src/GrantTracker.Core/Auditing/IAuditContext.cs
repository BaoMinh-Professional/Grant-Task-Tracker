namespace GrantTracker.Core.Auditing;

public interface IAuditContext
{
    AuditOutcome Outcome { get; }
    string? EntityType { get; }
    Guid? EntityId { get; }
    void SetTarget(string? entityType, Guid? entityId);
    void SetOutcome(AuditOutcome outcome);
}
