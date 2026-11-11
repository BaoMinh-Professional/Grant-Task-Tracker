namespace GrantTracker.Core.Common.Time;

public interface IBusinessClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}
