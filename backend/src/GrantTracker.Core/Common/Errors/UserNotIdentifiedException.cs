namespace GrantTracker.Core.Common.Errors;

public sealed class UserNotIdentifiedException(string code, string message) : AppException(code, message);
