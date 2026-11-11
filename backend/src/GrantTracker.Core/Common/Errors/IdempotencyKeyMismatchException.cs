namespace GrantTracker.Core.Common.Errors;

public sealed class IdempotencyKeyMismatchException()
    : AppException(ErrorCodes.IdempotencyKeyMismatch, "The idempotency key was already used for a different request.");
