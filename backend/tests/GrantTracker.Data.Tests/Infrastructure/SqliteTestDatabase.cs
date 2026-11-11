using GrantTracker.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Data.Tests.Infrastructure;

public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public async Task InitialiseAsync()
    {
        await connection.OpenAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public GrantTrackerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GrantTrackerDbContext>().UseSqlite(connection).Options);

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();
}
