using GrantTracker.Core.Common.Mediator;

namespace GrantTracker.Core.Grants.ListGrants;

public sealed record ListGrantsQuery(string? Status) : IQuery<IReadOnlyList<GrantListItemDto>>;
