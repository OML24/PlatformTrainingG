using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class LectureConfiguration : IEntityTypeConfiguration<Lecture>
{
    public void Configure(EntityTypeBuilder<Lecture> builder)
    {
        builder.ToTable("lectures", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_lectures_time_order", "ends_at > starts_at"));
        builder.ToTable(t => t.HasCheckConstraint("ck_lectures_code_digits", "attendance_code ~ '^[0-9]{4,8}$'"));
        builder.ToTable(t => t.HasCheckConstraint("ck_lectures_mode", "mode IN ('InPerson', 'Online')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by");
        builder.Property(x => x.UpdatedByUserId).HasColumnName("updated_by");
        builder.Property(x => x.Version).HasColumnName("xmin").IsRowVersion();
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(x => x.IsActive);
        builder.Property(x => x.DeactivatedByUserId).HasColumnName("deactivated_by");
        builder.Property(x => x.TitleAr).HasMaxLength(300);
        builder.Property(x => x.TitleEn).HasMaxLength(300);
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LocationAr).HasMaxLength(300);
        builder.Property(x => x.LocationEn).HasMaxLength(300);
        builder.Property(x => x.TeamsUrl).HasMaxLength(600);
        builder.Property(x => x.AttendanceCode).IsRequired().HasMaxLength(8);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CourseId, x.StartsAt });
    }
}

