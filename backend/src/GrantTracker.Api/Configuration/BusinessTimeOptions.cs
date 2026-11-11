namespace GrantTracker.Api.Configuration;

public sealed class BusinessTimeOptions
{
    public const string SectionName = "BusinessTime";
    public string TimeZoneId { get; set; } = "UTC";
}
