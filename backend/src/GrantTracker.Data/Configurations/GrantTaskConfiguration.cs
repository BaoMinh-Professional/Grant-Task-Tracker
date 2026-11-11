using GrantTracker.Core.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrantTracker.Data.Configurations;

public sealed class GrantTaskConfiguration : IEntityTypeConfiguration<GrantTask>
{
    public void Configure(EntityTypeBuilder<GrantTask> builder)
    {
        builder.ToTable("Tasks", table => table.HasCheckConstraint("CK_Tasks_Status_CompletedAt", "(\"Status\" = 'Completed') = (\"CompletedAtUtc\" IS NOT NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(10).HasConversion<string>().IsRequired();
        builder.Property(x => x.Status).HasMaxLength(10).HasConversion<string>().IsRequired();
        builder.Property(x => x.DueDate).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.HasIndex(x => new { x.Status, x.DueDate });
        builder.HasMany(x => x.Assignments).WithOne().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
    }
}
