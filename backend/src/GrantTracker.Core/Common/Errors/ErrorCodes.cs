namespace GrantTracker.Core.Common.Errors;

public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string MalformedRequest = "MALFORMED_REQUEST";
    public const string UserRequired = "USER_REQUIRED";
    public const string UnknownUser = "UNKNOWN_USER";
    public const string NotFound = "NOT_FOUND";
    public const string IdempotencyKeyRequired = "IDEMPOTENCY_KEY_REQUIRED";
    public const string IdempotencyKeyMismatch = "IDEMPOTENCY_KEY_MISMATCH";
    public const string UnexpectedError = "UNEXPECTED_ERROR";
    public const string TaskOnClosedGrant = "BR-1";
    public const string DueDateInvalid = "BR-2";
    public const string TitleInvalid = "BR-3";
    public const string CloseBlockedByHighPriorityTask = "BR-5";
    public const string GrantClosed = "GRANT_CLOSED";
    public const string TaskCompleted = "TASK_COMPLETED";
    public const string VersionConflict = "VERSION_CONFLICT";
    public const string PreconditionRequired = "PRECONDITION_REQUIRED";
}
