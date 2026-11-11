using GrantTracker.Core.Common.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GrantTracker.Data.Seeding;

public sealed class DbSeeder(GrantTrackerDbContext dbContext, IBusinessClock clock, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Database seed skipped because users already exist.");
            return;
        }

        foreach (var user in SeedData.Users)
        {
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        dbContext.Grants.AddRange(SeedData.CreateGrants(clock.Today, clock.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
        SeedData.AddTasks(dbContext, clock.Today, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.TaskAssignments.AddRange(SeedData.CreateTaskAssignments());
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.GrantAssignments.AddRange(SeedData.CreateGrantAssignments());
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Database seeded with demo users, grants and tasks.");
    }
}
