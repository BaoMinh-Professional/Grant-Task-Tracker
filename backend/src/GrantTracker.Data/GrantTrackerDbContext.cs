using GrantTracker.Core.Assignments;
using GrantTracker.Core.Auditing;
using GrantTracker.Core.Grants;
using GrantTracker.Core.Idempotency;
using GrantTracker.Core.Tasks;
using GrantTracker.Core.Users;
using GrantTracker.Data.Conversions;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Data;

public sealed class GrantTrackerDbContext(DbContextOptions<GrantTrackerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Grant> Grants => Set<Grant>();
    public DbSet<GrantTask> Tasks => Set<GrantTask>();
    public DbSet<GrantAssignment> GrantAssignments => Set<GrantAssignment>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<AuditEntry> Audits => Set<AuditEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcNullableDateTimeConverter>();
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GrantTrackerDbContext).Assembly);
}
