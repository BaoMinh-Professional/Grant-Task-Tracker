using GrantTracker.Api.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace GrantTracker.Api.Logging;

public static class SerilogConfiguration
{
    public static void Configure(IServiceProvider services, LoggerConfiguration configuration)
    {
        var appConfiguration = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        configuration.ReadFrom.Configuration(appConfiguration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console();
        var file = appConfiguration.GetSection(LogFileOptions.SectionName).Get<LogFileOptions>() ?? new LogFileOptions();
        if (!string.IsNullOrWhiteSpace(file.Path))
        {
            var path = Path.IsPathRooted(file.Path) ? file.Path : Path.Combine(environment.ContentRootPath, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            configuration.WriteTo.File(new CompactJsonFormatter(), path, rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: file.RetainedFileCountLimit, restrictedToMinimumLevel: LogEventLevel.Information);
        }
    }
}
