using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Mediator;

namespace GrantTracker.Core.Users.Login;

public sealed class LoginHandler(IUserRepository users) : IRequestHandler<LoginQuery, UserDto>
{
    public async Task<UserDto> Handle(LoginQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ValidationException(ErrorCodes.ValidationFailed, "Email is required.",
                new Dictionary<string, string[]> { ["email"] = ["Email is required."] });
        }

        var user = await users.FindByEmailAsync(EmailAddress.Normalise(request.Email), cancellationToken);
        if (user is null)
        {
            throw new UserNotIdentifiedException(ErrorCodes.UnknownUser, "The specified user was not found.");
        }

        return new UserDto(user.Id, user.Name, user.Email);
    }
}
