namespace GrantTracker.Core.Auditing;

public sealed class AuditEntry
{
    private AuditEntry() { }
    public AuditEntry(Guid id, DateTime occurredAtUtc, Guid userId, string action, string? entityType,
        Guid? entityId, AuditOutcome outcome, string? errorCode, string? traceId, string? idempotencyKey)
    {
        Id = id; OccurredAtUtc = occurredAtUtc; UserId = userId; Action = action; EntityType = entityType;
        EntityId = entityId; Outcome = outcome; ErrorCode = errorCode; TraceId = traceId; IdempotencyKey = idempotencyKey;
    }
    public Guid Id { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public Guid UserId { get; private set; }
    public string Action { get; private set; } = "";
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public AuditOutcome Outcome { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? TraceId { get; private set; }
    public string? IdempotencyKey { get; private set; }
}
