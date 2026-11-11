using GrantTracker.Core.Common.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Tests.Infrastructure;

[ApiController]
[Route("__test")]
public sealed class TestEndpointsController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("whoami")]
    public IActionResult WhoAmI() => Ok(new { currentUser.UserId, currentUser.Name });

    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException("test-only failure");
}
