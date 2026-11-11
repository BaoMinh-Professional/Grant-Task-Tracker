namespace GrantTracker.Core.Common.Errors;

public sealed class ConflictException(string code, string message) : AppException(code, message);
