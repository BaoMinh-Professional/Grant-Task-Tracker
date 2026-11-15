using Microsoft.Extensions.Logging;

namespace GrantTracker.Core.Tests.Common;

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

internal sealed class ScopeSpy(object? state) : IDisposable
{
    public object? State { get; } = state;
    public bool Disposed { get; private set; }
    public void Dispose() => Disposed = true;
}

// Records what a behaviour logs so tests can assert on levels, messages and scopes without a logging package.
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];
    public List<ScopeSpy> Scopes { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var scope = new ScopeSpy(state);
        Scopes.Add(scope);
        return scope;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
}
