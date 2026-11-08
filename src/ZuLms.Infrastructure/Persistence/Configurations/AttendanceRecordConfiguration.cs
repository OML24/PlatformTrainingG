using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("attendance_records", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_attendance_records_source", "source IN ('Code', 'Manual')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(10);
        builder.HasOne(x => x.Lecture).WithMany().HasForeignKey(x => x.LectureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Participant).WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MarkedByUser).WithMany().HasForeignKey(x => x.MarkedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LectureId, x.ParticipantId }).IsUnique();
    }
}

