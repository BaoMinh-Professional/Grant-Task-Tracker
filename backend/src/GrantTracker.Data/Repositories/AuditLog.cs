using GrantTracker.Core.Auditing;

namespace GrantTracker.Data.Repositories;

public sealed class AuditLog(GrantTrackerDbContext dbContext) : IAuditLog
{
    public void Add(AuditEntry entry) => dbContext.Audits.Add(entry);
}
