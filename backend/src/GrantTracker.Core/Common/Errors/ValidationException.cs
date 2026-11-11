namespace GrantTracker.Core.Common.Errors;

public sealed class ValidationException : AppException
{
    public ValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
        : base(ErrorCodes.ValidationFailed, message) => Errors = errors;

    public ValidationException(string code, string message, IReadOnlyDictionary<string, string[]> errors)
        : base(code, message) => Errors = errors;

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
