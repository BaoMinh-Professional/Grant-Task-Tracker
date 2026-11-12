using GrantTracker.Core.Common.Time;

namespace GrantTracker.Api.Tests.Infrastructure;

public sealed class FakeBusinessClock : IBusinessClock
{
    public DateTime UtcNow { get; set; } = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    public DateOnly Today { get; set; } = new(2026, 3, 10);
}
