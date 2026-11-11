using GrantTracker.Data.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrantTracker.Data.Tests;

public sealed class MigrationTests
{
    [Fact]
    public async Task MigrationsApplyAndMatchTheModel()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitialiseAsync();
        await using var context = database.CreateContext();
        var differ = context.GetService<IMigrationsModelDiffer>();
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var initializer = context.GetService<IModelRuntimeInitializer>();
        var snapshot = initializer.Initialize(migrationsAssembly.ModelSnapshot!.Model, designTime: true);
        var designModel = context.GetService<IDesignTimeModel>().Model;
        Assert.Empty(differ.GetDifferences(snapshot.GetRelationalModel(), designModel.GetRelationalModel()));
    }
}
