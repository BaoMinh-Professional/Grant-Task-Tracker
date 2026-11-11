namespace GrantTracker.Core.Idempotency;

public interface IIdempotentCommand
{
    string? IdempotencyKey { get; }
}
