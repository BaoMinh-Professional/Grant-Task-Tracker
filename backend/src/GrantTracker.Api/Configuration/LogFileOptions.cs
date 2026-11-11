namespace GrantTracker.Api.Configuration;

public sealed class LogFileOptions
{
    public const string SectionName = "LogFile";
    public string Path { get; set; } = "";
    public int RetainedFileCountLimit { get; set; } = 14;
}
