namespace GrantTracker.Core.Common.Errors;

public sealed class UniqueConstraintViolationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
