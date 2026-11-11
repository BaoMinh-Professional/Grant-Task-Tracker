namespace GrantTracker.Core.Auditing;

public interface IAuditLog
{
    void Add(AuditEntry entry);
}
