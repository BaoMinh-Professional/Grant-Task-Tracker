namespace GrantTracker.Core.Common.Errors;

public abstract class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
