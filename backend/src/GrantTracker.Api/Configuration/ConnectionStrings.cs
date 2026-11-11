using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace GrantTracker.Api.Configuration;

public static class ConnectionStrings
{
    public static string Resolve(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var raw = configuration.GetConnectionString("GrantTracker")
            ?? throw new InvalidOperationException("ConnectionStrings:GrantTracker is required.");
        var builder = new SqliteConnectionStringBuilder(raw);
        if (builder.DataSource is "" or ":memory:" || string.Equals(builder.Mode.ToString(), "Memory", StringComparison.OrdinalIgnoreCase))
            return builder.ToString();
        if (!Path.IsPathRooted(builder.DataSource))
            builder.DataSource = Path.GetFullPath(Path.Combine(environment.ContentRootPath, builder.DataSource));
        return builder.ToString();
    }
}
