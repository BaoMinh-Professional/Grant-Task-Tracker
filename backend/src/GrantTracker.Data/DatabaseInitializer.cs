using GrantTracker.Data.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GrantTracker.Data;

public sealed class DatabaseInitializer(GrantTrackerDbContext dbContext, DbSeeder seeder, ILogger<DatabaseInitializer> logger)
{
    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = dbContext.Database.GetConnectionString() ?? "";
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (builder.DataSource is { Length: > 0 } source && source != ":memory:" && !string.Equals(builder.Mode.ToString(), "Memory", StringComparison.OrdinalIgnoreCase))
        {
            var fullPath = Path.GetFullPath(source);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        }
        await dbContext.Database.MigrateAsync(cancellationToken);
        await seeder.SeedAsync(cancellationToken);
        logger.LogInformation("Database migrations applied.");
    }
}
