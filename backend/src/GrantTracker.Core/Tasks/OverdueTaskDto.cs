using GrantTracker.Core.Grants;

namespace GrantTracker.Core.Tasks;

public sealed record OverdueTaskDto(Guid Id, Guid GrantId, string GrantReferenceNumber, string OrganisationName,
    GrantStatus GrantStatus, string Title, TaskPriority Priority, DateOnly DueDate, int DaysOverdue, int Version);
