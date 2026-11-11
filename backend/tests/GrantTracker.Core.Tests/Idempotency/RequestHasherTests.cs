using GrantTracker.Core.Idempotency;

namespace GrantTracker.Core.Tests.Idempotency;

public sealed class RequestHasherTests
{
    private sealed record Command(string Value, string? IdempotencyKey) : IIdempotentCommand;

    [Fact]
    public void OmitsKeyAndChangesWithBody()
    {
        var first = RequestHasher.Hash(new Command("value", "first"));
        Assert.Equal(first, RequestHasher.Hash(new Command("value", "second")));
        Assert.NotEqual(first, RequestHasher.Hash(new Command("other", "first")));
        Assert.Matches("^[0-9a-f]{64}$", first);
    }
}
