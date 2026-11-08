using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("certificates", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_certificates_status", "status IN ('Issued', 'Revoked')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_certificates_revoked_reason", "revoked_reason IN ('AttendanceCorrection', 'ExamResultChanged', 'ExamRequirementChanged', 'CourseCancelled', 'CourseReopened', 'ParticipantRemoved', 'CourseDisabledRestoreIneligible', 'ManualByAdmin')"));
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
        builder.Property(x => x.ReferenceNo).IsRequired().HasMaxLength(40);
        builder.Property(x => x.VerificationToken).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.PdfRenderVersion).HasDefaultValue(1);
        builder.Property(x => x.HolderNameAr).IsRequired().HasMaxLength(400);
        builder.Property(x => x.HolderNameEn).HasMaxLength(400);
        builder.Property(x => x.CourseTitleAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.CourseTitleEn).HasMaxLength(300);
        builder.Property(x => x.CoursePeriodText).IsRequired().HasMaxLength(200);
        builder.Property(x => x.InstructorNameAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.InstructorNameEn).HasMaxLength(300);
        builder.Property(x => x.RevokedReason).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Participant).WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PdfFile).WithMany().HasForeignKey(x => x.PdfFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ReferenceNo).IsUnique();
        builder.HasIndex(x => x.VerificationToken).IsUnique();
        builder.HasIndex(x => x.ParticipantId).IsUnique().HasFilter("status = 'Issued'");
        builder.HasIndex(x => new { x.UserId, x.Status });
        builder.HasIndex(x => x.CourseId);
    }
}

