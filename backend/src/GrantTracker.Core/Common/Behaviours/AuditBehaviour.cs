using System.Diagnostics;
using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Identity;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Common.Persistence;
using GrantTracker.Core.Common.Time;
using GrantTracker.Core.Idempotency;
using Microsoft.Extensions.Logging;

namespace GrantTracker.Core.Common.Behaviours;

public sealed class AuditBehaviour<TRequest, TResponse>(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IAuditLog auditLog,
    IAuditContext auditContext,
    IBusinessClock clock,
    ILogger<AuditBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehaviour<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICommand) return await next();
        var userId = currentUser.UserId ?? throw new InvalidOperationException("Commands require an identified user.");
        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            var result = await next();
            auditLog.Add(CreateEntry(request, userId, auditContext.Outcome, null));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (AppException exception)
        {
            unitOfWork.DiscardChanges();
            auditLog.Add(CreateEntry(request, userId, AuditOutcome.Rejected, exception.Code));
            try { await unitOfWork.SaveChangesAsync(cancellationToken); }
            catch (Exception auditException) { logger.LogError(auditException, "Failed to write rejection audit entry."); }
            throw;
        }
    }

    private AuditEntry CreateEntry(TRequest request, Guid userId, AuditOutcome outcome, string? errorCode)
    {
        var name = typeof(TRequest).Name;
        if (name.EndsWith("Command", StringComparison.Ordinal)) name = name[..^"Command".Length];
        return new AuditEntry(Guid.NewGuid(), clock.UtcNow, userId, name, auditContext.EntityType,
            auditContext.EntityId, outcome, errorCode, Activity.Current?.TraceId.ToHexString(),
            (request as IIdempotentCommand)?.IdempotencyKey?.Trim());
    }
}
