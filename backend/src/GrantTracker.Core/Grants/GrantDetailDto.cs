using GrantTracker.Core.Tasks;

namespace GrantTracker.Core.Grants;

public sealed record GrantDetailDto(Guid Id, string ReferenceNumber, string OrganisationName,
    DateOnly ApplicationDate, GrantStatus Status, DateOnly Today, int PendingHighTasks, bool CanClose,
    IReadOnlyList<TaskDto> Tasks);
