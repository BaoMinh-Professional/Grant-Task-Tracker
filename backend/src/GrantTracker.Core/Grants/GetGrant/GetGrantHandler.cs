using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Common.Time;
using GrantTracker.Core.Tasks;

namespace GrantTracker.Core.Grants.GetGrant;

public sealed class GetGrantHandler(IGrantRepository grants, IBusinessClock clock) : IRequestHandler<GetGrantQuery, GrantDetailDto>
{
    public async Task<GrantDetailDto> Handle(GetGrantQuery request, CancellationToken cancellationToken)
    {
        var grant = await grants.FindWithTasksAsync(request.GrantId, cancellationToken)
            ?? throw new NotFoundException("Grant not found.");
        var today = clock.Today;
        var tasks = grant.Tasks
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Title)
            .Select(t => TaskDto.From(t, today))
            .ToList();
        return new GrantDetailDto(grant.Id, grant.ReferenceNumber, grant.OrganisationName, grant.ApplicationDate,
            grant.Status, today, grant.PendingHighTaskCount, grant.CanClose, tasks);
    }
}
