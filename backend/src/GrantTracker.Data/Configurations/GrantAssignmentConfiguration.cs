using GrantTracker.Core.Assignments;
using GrantTracker.Core.Grants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrantTracker.Data.Configurations;

// Present but not used by the grant and task features (assignment is out of scope).
public sealed class GrantAssignmentConfiguration : IEntityTypeConfiguration<GrantAssignment>
{
    public void Configure(EntityTypeBuilder<GrantAssignment> builder)
    {
        builder.ToTable("AssignUserGrant");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.GrantId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasOne<Grant>().WithMany(x => x.Assignments).HasForeignKey(x => x.GrantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GrantTracker.Core.Users.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
