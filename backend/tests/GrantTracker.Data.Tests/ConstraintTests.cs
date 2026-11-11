using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Grants;
using GrantTracker.Core.Tasks;
using GrantTracker.Core.Users;
using GrantTracker.Data;
using GrantTracker.Data.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace GrantTracker.Data.Tests;

public sealed class ConstraintTests
{
    [Fact]
    public async Task UniqueEmailIsTranslatedToCoreException()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitialiseAsync();
        await using var context = database.CreateContext();
        context.Users.AddRange(new User(Guid.NewGuid(), "One", "same@example.org"), new User(Guid.NewGuid(), "Two", "same@example.org"));
        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => new EfUnitOfWork(context).SaveChangesAsync());
    }

    [Fact]
    public async Task InconsistentStatusTimestampIsRejected()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitialiseAsync();
        await using var context = database.CreateContext();
        var grant = new Grant(Guid.NewGuid(), "GR-1", "Org", new DateOnly(2026, 1, 1), GrantStatus.Open, null);
        await context.Database.ExecuteSqlAsync($"INSERT INTO Grants (Id, ReferenceNumber, OrganisationName, ApplicationDate, Status, ClosedAtUtc) VALUES ({grant.Id}, {grant.ReferenceNumber}, {grant.OrganisationName}, {grant.ApplicationDate.ToString("yyyy-MM-dd")}, {"Open"}, {null})");
        var exception = await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlAsync($"INSERT INTO Tasks (Id, Title, GrantId, DueDate, Priority, Status, CompletedAtUtc, CreatedAtUtc) VALUES ({Guid.NewGuid()}, {"Task"}, {grant.Id}, {"2026-01-02"}, {"High"}, {"Completed"}, {null}, {DateTime.UtcNow})"));
        Assert.Equal(275, exception.SqliteExtendedErrorCode);
    }
}
