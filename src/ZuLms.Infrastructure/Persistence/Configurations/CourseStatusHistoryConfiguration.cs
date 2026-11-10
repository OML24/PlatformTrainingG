using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class CourseStatusHistoryConfiguration : IEntityTypeConfiguration<CourseStatusHistory>
{
    public void Configure(EntityTypeBuilder<CourseStatusHistory> builder)
    {
        builder.ToTable("course_status_history", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_course_status_history_from_status", "from_status IN ('Proposed', 'Returned', 'Rejected', 'Approved', 'Finished', 'Cancelled', 'AwaitingAccount')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_course_status_history_to_status", "to_status IN ('Proposed', 'Returned', 'Rejected', 'Approved', 'Finished', 'Cancelled', 'AwaitingAccount')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CourseId, x.ChangedAt });
    }
}

