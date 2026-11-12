using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Common.Time;

namespace GrantTracker.Core.Grants.CloseGrant;

public sealed class CloseGrantHandler(IGrantRepository grants, IBusinessClock clock, IAuditContext audit)
    : IRequestHandler<CloseGrantCommand, GrantListItemDto>
{
    public async Task<GrantListItemDto> Handle(CloseGrantCommand request, CancellationToken cancellationToken)
    {
        audit.SetTarget("Grant", request.GrantId);
        var grant = await grants.FindWithTasksAsync(request.GrantId, cancellationToken)
            ?? throw new NotFoundException("Grant not found.");
        if (!grant.Close(clock.UtcNow)) audit.SetOutcome(AuditOutcome.NoChange);
        return GrantListItemDto.From(grant);
    }
}
