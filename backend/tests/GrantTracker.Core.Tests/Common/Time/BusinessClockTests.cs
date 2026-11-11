using GrantTracker.Core.Common.Time;
using Microsoft.Extensions.Time.Testing;

namespace GrantTracker.Core.Tests.Common.Time;

public sealed class BusinessClockTests
{
    [Fact]
    public void UsesBusinessTimezoneForCalendarDate()
    {
        var provider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 10, 13, 59, 59, TimeSpan.Zero));
        var clock = new BusinessClock(provider, TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane"));
        Assert.Equal(new DateOnly(2026, 3, 10), clock.Today);
        provider.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(new DateOnly(2026, 3, 11), clock.Today);
        Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
    }
}
