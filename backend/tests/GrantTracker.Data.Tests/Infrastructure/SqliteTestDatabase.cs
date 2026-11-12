using GrantTracker.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrantTracker.Data.Tests.Infrastructure;

public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public async Task InitialiseAsync(string? targetMigration = null)
    {
        await connection.OpenAsync();
        await using var db = CreateContext();
        if (targetMigration is not null)
            await db.GetService<IMigrator>().MigrateAsync(targetMigration);
        else
            await db.Database.MigrateAsync();
    }

    public GrantTrackerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GrantTrackerDbContext>().UseSqlite(connection).Options);

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();
}
