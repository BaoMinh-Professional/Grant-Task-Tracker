using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Users;
using Serilog.Context;
using Serilog;

namespace GrantTracker.Api.Identity;

public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUserRepository users, HttpCurrentUser currentUser, IDiagnosticContext diagnosticContext)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null || endpoint.Metadata.GetMetadata<AllowAnonymousUserAttribute>() is not null)
        {
            await next(context);
            return;
        }

        var header = context.Request.Headers[HeaderNames.UserId].ToString();
        if (string.IsNullOrWhiteSpace(header))
            throw new UserNotIdentifiedException(ErrorCodes.UserRequired, "The X-User-Id header is required.");
        if (!Guid.TryParse(header, out var userId))
            throw new UserNotIdentifiedException(ErrorCodes.UnknownUser, "The specified user was not found.");
        var user = await users.FindByIdAsync(userId, context.RequestAborted);
        if (user is null)
            throw new UserNotIdentifiedException(ErrorCodes.UnknownUser, "The specified user was not found.");

        currentUser.Set(user);
        diagnosticContext.Set("UserId", user.Id);
        using (LogContext.PushProperty("UserId", user.Id))
            await next(context);
    }
}
