using GrantTracker.Core.Common.Mediator;

namespace GrantTracker.Core.Grants.CloseGrant;

public sealed record CloseGrantCommand(Guid GrantId) : ICommand<GrantListItemDto>;
