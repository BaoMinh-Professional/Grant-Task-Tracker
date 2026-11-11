namespace GrantTracker.Core.Common.Time;

public sealed class BusinessClock(TimeProvider timeProvider, TimeZoneInfo businessTimeZone) : IBusinessClock
{
    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), businessTimeZone).DateTime);
}
