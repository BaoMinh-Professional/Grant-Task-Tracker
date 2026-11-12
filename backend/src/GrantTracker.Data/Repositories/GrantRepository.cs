using GrantTracker.Core.Grants;
using GrantTracker.Core.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Data.Repositories;

public sealed class GrantRepository(GrantTrackerDbContext db) : IGrantRepository
{
    public Task<Grant?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Grants.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);

    public Task<Grant?> FindWithTasksAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Grants.Include(g => g.Tasks).SingleOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<IReadOnlyList<GrantSummary>> ListAsync(GrantStatus? status, CancellationToken cancellationToken = default)
    {
        var query = db.Grants.AsNoTracking();
        if (status is { } wanted) query = query.Where(g => g.Status == wanted);
        return await query
            .OrderBy(g => g.Status == GrantStatus.Open ? 0 : 1)
            .ThenBy(g => g.ApplicationDate)
            .ThenBy(g => g.ReferenceNumber)
            .Select(g => new GrantSummary(g.Id, g.ReferenceNumber, g.OrganisationName, g.ApplicationDate, g.Status,
                g.Tasks.Count,
                g.Tasks.Count(t => t.Status == GrantTaskStatus.Completed),
                g.Tasks.Count(t => t.Status == GrantTaskStatus.Pending && t.Priority == TaskPriority.High)))
            .ToListAsync(cancellationToken);
    }
}
