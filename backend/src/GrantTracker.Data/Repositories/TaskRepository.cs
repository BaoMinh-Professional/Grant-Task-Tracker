using GrantTracker.Core.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Data.Repositories;

public sealed class TaskRepository(GrantTrackerDbContext db) : ITaskRepository
{
    public void Add(GrantTask task) => db.Tasks.Add(task);

    public Task<GrantTask?> FindWithGrantAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Tasks.Include(t => t.Grant).SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<GrantTask>> ListOverdueAsync(DateOnly today, CancellationToken cancellationToken = default) =>
        await db.Tasks.AsNoTracking().Include(t => t.Grant)
            .Where(t => t.Status == GrantTaskStatus.Pending && t.DueDate < today)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Priority == TaskPriority.High ? 0 : t.Priority == TaskPriority.Medium ? 1 : 2)
            .ThenBy(t => t.Title)
            .ToListAsync(cancellationToken);
}
