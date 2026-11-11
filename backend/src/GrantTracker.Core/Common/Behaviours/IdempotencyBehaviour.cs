using System.Text.Json;
using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Common.Persistence;
using GrantTracker.Core.Common.Time;
using GrantTracker.Core.Idempotency;

namespace GrantTracker.Core.Common.Behaviours;

public sealed class IdempotencyBehaviour<TRequest, TResponse>(
    IIdempotencyStore store,
    IUnitOfWork unitOfWork,
    IBusinessClock clock,
    IAuditContext auditContext)
    : IPipelineBehaviour<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IIdempotentCommand command) return await next();
        var key = command.IdempotencyKey?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length > 100)
            throw new ValidationException(ErrorCodes.IdempotencyKeyRequired, "A valid Idempotency-Key header is required.",
                new Dictionary<string, string[]> { ["Idempotency-Key"] = ["The key must contain between 1 and 100 characters."] });

        var operation = typeof(TRequest).Name;
        var hash = RequestHasher.Hash(request);
        var existing = await store.FindAsync(key, cancellationToken);
        if (existing is not null) return Replay(existing, operation, hash);

        var result = await next();
        store.Add(new IdempotencyRecord(key, operation, hash, JsonSerializer.Serialize(result), clock.UtcNow));
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (UniqueConstraintViolationException)
        {
            unitOfWork.DiscardChanges();
            existing = await store.FindAsync(key, cancellationToken)
                ?? throw new InvalidOperationException("The winning idempotency record could not be found.");
            return Replay(existing, operation, hash);
        }

        return result;
    }

    private TResponse Replay(IdempotencyRecord record, string operation, string hash)
    {
        if (record.Operation != operation || record.RequestHash != hash) throw new IdempotencyKeyMismatchException();
        auditContext.SetOutcome(AuditOutcome.Replayed);
        return JsonSerializer.Deserialize<TResponse>(record.ResultJson)
            ?? throw new InvalidOperationException("The stored idempotency result was empty.");
    }
}
