using GrantTracker.Api.Identity;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Users;
using GrantTracker.Core.Users.Login;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Auth;

[ApiController]
[Route("auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymousUser]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send<LoginQuery, UserDto>(new LoginQuery(request.Email), cancellationToken));
}
