namespace GrantTracker.Core.Common.Errors;

public sealed class NotFoundException(string message) : AppException(ErrorCodes.NotFound, message);
