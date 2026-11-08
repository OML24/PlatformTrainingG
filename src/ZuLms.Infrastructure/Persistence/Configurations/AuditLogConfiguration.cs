using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_audit_logs_category", "category IN ('Auth', 'User', 'Permission', 'Settings', 'Course', 'Lecture', 'Attendance', 'Exam', 'Impact', 'Evaluation', 'Certificate', 'Document', 'Sync', 'Need', 'System', 'Security')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.ActorLogin).HasMaxLength(40);
        builder.Property(x => x.ActorName).HasMaxLength(400);
        builder.Property(x => x.ActorRoles).HasMaxLength(200);
        builder.Property(x => x.Ip).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.DeviceSummary).HasMaxLength(200);
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ActionCode).IsRequired().HasMaxLength(80);
        builder.Property(x => x.EntityType).HasMaxLength(60);
        builder.Property(x => x.EntityLabel).HasMaxLength(300);
        builder.Property(x => x.SummaryAr).IsRequired().HasMaxLength(500);
        builder.Property(x => x.SummaryEn).HasMaxLength(500);
        builder.Property(x => x.BeforeJson).HasColumnType("jsonb");
        builder.Property(x => x.AfterJson).HasColumnType("jsonb");
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => new { x.ActorUserId, x.OccurredAt });
        builder.HasIndex(x => new { x.Category, x.OccurredAt });
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => new { x.CourseId, x.OccurredAt });
        builder.HasIndex(x => x.ActionCode);
    }
}

