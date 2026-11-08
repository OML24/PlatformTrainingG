using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class AttendanceCodeAttemptConfiguration : IEntityTypeConfiguration<AttendanceCodeAttempt>
{
    public void Configure(EntityTypeBuilder<AttendanceCodeAttempt> builder)
    {
        builder.ToTable("attendance_code_attempts", AppDbContext.Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.HasOne(x => x.Lecture).WithMany().HasForeignKey(x => x.LectureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LectureId, x.UserId }).IsUnique();
    }
}

