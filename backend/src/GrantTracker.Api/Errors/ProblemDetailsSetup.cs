using GrantTracker.Core.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Errors;

public static class ProblemDetailsSetup
{
    public static void Customize(ProblemDetailsContext context)
    {
        var details = context.ProblemDetails;
        details.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToHexString()
            ?? context.HttpContext.TraceIdentifier;
        if (!details.Extensions.ContainsKey("code"))
            details.Extensions["code"] = details.Status switch
            {
                StatusCodes.Status400BadRequest => ErrorCodes.MalformedRequest,
                StatusCodes.Status401Unauthorized => ErrorCodes.UserRequired,
                StatusCodes.Status404NotFound => ErrorCodes.NotFound,
                _ => ErrorCodes.UnexpectedError
            };
    }

    public static IActionResult InvalidModelState(ActionContext context)
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The request is invalid."
        };
        problem.Extensions["code"] = ErrorCodes.MalformedRequest;
        problem.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToHexString()
            ?? context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }
}
