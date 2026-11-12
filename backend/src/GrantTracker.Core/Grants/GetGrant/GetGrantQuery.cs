using GrantTracker.Core.Common.Mediator;

namespace GrantTracker.Core.Grants.GetGrant;

public sealed record GetGrantQuery(Guid GrantId) : IQuery<GrantDetailDto>;
