using GrantTracker.Core.Common.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Errors;

public sealed class AppExceptionHandler(
    ILogger<AppExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = exception switch
        {
            ValidationException validation => (400, validation.Code, validation.Message),
            UserNotIdentifiedException identity => (401, identity.Code, identity.Message),
            NotFoundException notFound => (404, notFound.Code, notFound.Message),
            ConflictException conflict => (409, conflict.Code, conflict.Message),
            IdempotencyKeyMismatchException mismatch => (422, mismatch.Code, mismatch.Message),
            _ => (500, ErrorCodes.UnexpectedError, "An unexpected error occurred.")
        };
        if (status == 500) logger.LogError(exception, "Unhandled exception for trace {TraceId}.", httpContext.TraceIdentifier);
        else logger.LogWarning("Request failed with {Code} for trace {TraceId}.", code, httpContext.TraceIdentifier);

        var problem = new ProblemDetails { Status = status, Title = title, Instance = httpContext.Request.Path };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToHexString() ?? httpContext.TraceIdentifier;
        if (exception is ValidationException validationException)
        {
            problem = new ValidationProblemDetails(validationException.Errors.ToDictionary())
            {
                Status = status, Title = title, Instance = httpContext.Request.Path
            };
            problem.Extensions["code"] = code;
            problem.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToHexString() ?? httpContext.TraceIdentifier;
        }
        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
