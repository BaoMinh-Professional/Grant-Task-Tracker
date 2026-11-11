namespace GrantTracker.Core.Common.Identity;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Name { get; }
}
