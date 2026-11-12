using GrantTracker.Core.Assignments;
using GrantTracker.Core.Grants;
using GrantTracker.Core.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrantTracker.Data.Configurations;

// Present but not used by the grant and task features (assignment is out of scope).
public sealed class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.ToTable("AssignUserTask");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TaskId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasOne<GrantTask>().WithMany(x => x.Assignments).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GrantTracker.Core.Users.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
