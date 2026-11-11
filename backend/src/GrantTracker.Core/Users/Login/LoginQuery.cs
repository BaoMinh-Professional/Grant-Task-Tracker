using GrantTracker.Core.Common.Mediator;

namespace GrantTracker.Core.Users.Login;

public sealed record LoginQuery(string? Email) : IQuery<UserDto>;
