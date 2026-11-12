namespace GrantTracker.Core.Grants;

public sealed record GrantSummary(Guid Id, string ReferenceNumber, string OrganisationName,
    DateOnly ApplicationDate, GrantStatus Status, int TotalTasks, int CompletedTasks, int PendingHighTasks);

public interface IGrantRepository
{
    Task<Grant?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Grant?> FindWithTasksAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GrantSummary>> ListAsync(GrantStatus? status, CancellationToken cancellationToken = default);
}
