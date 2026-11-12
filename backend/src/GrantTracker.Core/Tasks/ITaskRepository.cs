namespace GrantTracker.Core.Tasks;

public interface ITaskRepository
{
    void Add(GrantTask task);
    Task<GrantTask?> FindWithGrantAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GrantTask>> ListOverdueAsync(DateOnly today, CancellationToken cancellationToken = default);
}
