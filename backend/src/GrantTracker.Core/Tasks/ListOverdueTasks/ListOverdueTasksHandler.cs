using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Common.Time;

namespace GrantTracker.Core.Tasks.ListOverdueTasks;

public sealed class ListOverdueTasksHandler(ITaskRepository tasks, IBusinessClock clock)
    : IRequestHandler<ListOverdueTasksQuery, IReadOnlyList<OverdueTaskDto>>
{
    public async Task<IReadOnlyList<OverdueTaskDto>> Handle(ListOverdueTasksQuery request, CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var overdue = await tasks.ListOverdueAsync(today, cancellationToken);
        return overdue.Select(t =>
        {
            var grant = t.Grant!;
            return new OverdueTaskDto(t.Id, t.GrantId, grant.ReferenceNumber, grant.OrganisationName, grant.Status,
                t.Title, t.Priority, t.DueDate, t.DaysOverdue(today), t.Version);
        }).ToList();
    }
}
