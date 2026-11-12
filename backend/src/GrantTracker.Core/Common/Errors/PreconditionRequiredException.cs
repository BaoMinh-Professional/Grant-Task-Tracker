namespace GrantTracker.Core.Common.Errors;

public sealed class PreconditionRequiredException() : AppException(ErrorCodes.PreconditionRequired,
    "Send the task's current version in the If-Match header.");
