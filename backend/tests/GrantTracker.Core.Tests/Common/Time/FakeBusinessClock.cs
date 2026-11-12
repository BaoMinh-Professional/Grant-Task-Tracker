using GrantTracker.Core.Common.Time;

namespace GrantTracker.Core.Tests.Common.Time;

internal sealed class FakeBusinessClock(DateTime utcNow, DateOnly today) : IBusinessClock
{
    public DateTime UtcNow { get; } = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    public DateOnly Today { get; } = today;
}
