using GrantTracker.Core.Tasks;

namespace GrantTracker.Core.Grants;

public sealed record GrantListItemDto(Guid Id, string ReferenceNumber, string OrganisationName,
    DateOnly ApplicationDate, GrantStatus Status, int TotalTasks, int CompletedTasks,
    int PendingHighTasks, bool CanClose)
{
    public static GrantListItemDto From(GrantSummary s) => new(s.Id, s.ReferenceNumber, s.OrganisationName,
        s.ApplicationDate, s.Status, s.TotalTasks, s.CompletedTasks, s.PendingHighTasks,
        GrantRules.CanClose(s.Status, s.PendingHighTasks));

    public static GrantListItemDto From(Grant g) => new(g.Id, g.ReferenceNumber, g.OrganisationName,
        g.ApplicationDate, g.Status, g.Tasks.Count,
        g.Tasks.Count(t => t.Status == GrantTaskStatus.Completed), g.PendingHighTaskCount, g.CanClose);
}
