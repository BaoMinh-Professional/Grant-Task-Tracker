using GrantTracker.Core.Common.Identity;
using GrantTracker.Core.Users;

namespace GrantTracker.Api.Identity;

public sealed class HttpCurrentUser : ICurrentUser
{
    public Guid? UserId { get; private set; }
    public string? Name { get; private set; }
    public void Set(User user) { UserId = user.Id; Name = user.Name; }
}
