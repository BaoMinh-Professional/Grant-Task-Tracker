using GrantTracker.Core.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrantTracker.Data.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("Audits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(50);
        builder.Property(x => x.Outcome).HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(50);
        builder.Property(x => x.TraceId).HasMaxLength(32);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100);
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasOne<GrantTracker.Core.Users.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
