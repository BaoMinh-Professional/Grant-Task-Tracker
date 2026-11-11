using GrantTracker.Core.Grants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrantTracker.Data.Configurations;

public sealed class GrantConfiguration : IEntityTypeConfiguration<Grant>
{
    public void Configure(EntityTypeBuilder<Grant> builder)
    {
        builder.ToTable("Grants", table => table.HasCheckConstraint("CK_Grants_Status_ClosedAt", "(\"Status\" = 'Closed') = (\"ClosedAtUtc\" IS NOT NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.ReferenceNumber).IsUnique();
        builder.Property(x => x.OrganisationName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(10).HasConversion<string>().IsRequired();
        builder.Property(x => x.ApplicationDate).IsRequired();
        builder.HasMany(x => x.Tasks).WithOne(x => x.Grant).HasForeignKey(x => x.GrantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Assignments).WithOne().HasForeignKey(x => x.GrantId).OnDelete(DeleteBehavior.Restrict);
    }
}
