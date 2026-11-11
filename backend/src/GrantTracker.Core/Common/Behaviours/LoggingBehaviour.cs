using System.Diagnostics;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Identity;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Idempotency;
using Microsoft.Extensions.Logging;

namespace GrantTracker.Core.Common.Behaviours;

public sealed class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger,
    ICurrentUser currentUser)
    : IPipelineBehaviour<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var idempotencyKey = (request as IIdempotentCommand)?.IdempotencyKey;
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Request"] = typeof(TRequest).Name,
            ["UserId"] = currentUser.UserId,
            ["IdempotencyKey"] = idempotencyKey
        });
        var timer = Stopwatch.StartNew();
        try
        {
            var result = await next();
            logger.LogInformation("Handled {Request} in {ElapsedMs} ms", typeof(TRequest).Name, timer.ElapsedMilliseconds);
            return result;
        }
        catch (AppException exception)
        {
            logger.LogWarning("Rejected {Request} with {ErrorCode}", typeof(TRequest).Name, exception.Code);
            throw;
        }
    }
}
