namespace GrantTracker.Core.Common.Errors;

public sealed class VersionConflictException() : AppException(ErrorCodes.VersionConflict,
    "This task was changed by someone else before your edit was saved. Reload to see the latest version, then edit again.");
