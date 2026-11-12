using GrantTracker.Core.Idempotency;
using GrantTracker.Core.Tests.Common;
using GrantTracker.Core.Tests.Common.Time;
using Moq;

namespace GrantTracker.Core.Tests.Idempotency;

public sealed class IdempotencyCleanupTests
{
    [Fact]
    public async Task PurgesRecordsOlderThanTheRetentionWindow()
    {
        var clock = new FakeBusinessClock(TestGrants.Now, TestGrants.Today);
        var store = new Mock<IIdempotencyStore>();
        store.Setup(x => x.DeleteCreatedBeforeAsync(clock.UtcNow - IdempotencyCleanup.Retention, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var purged = await new IdempotencyCleanup(store.Object, clock).PurgeExpiredAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, purged);
    }
}
