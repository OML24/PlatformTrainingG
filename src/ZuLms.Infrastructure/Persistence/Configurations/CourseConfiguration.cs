using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("courses", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_hours_positive", "total_hours > 0"));
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_planned_dates", "planned_end_date >= planned_start_date"));
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_max_participants", "max_participants IS NULL OR max_participants > 0"));
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_attendance_override", "attendance_threshold_percent_override IS NULL OR attendance_threshold_percent_override BETWEEN 1 AND 100"));
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_instructor_ref", "(instructor_kind IN ('Internal', 'ExternalAccount')) = (instructor_user_id IS NOT NULL)"));
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_external_managed", "instructor_kind <> 'ExternalNameOnly' OR managed_by_quality"));
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_faculty_target", "audience_type <> 'Faculty' OR target_center_id IS NOT NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_dean_origin", "origin <> 'DeanProposal' OR proposing_center_id IS NOT NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_delivery_mode", "delivery_mode IN ('InPerson', 'Online', 'Blended')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_origin", "origin IN ('DeanProposal', 'QualityCreated', 'AdminCreated', 'Historical')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_audience_type", "audience_type IN ('General', 'Faculty', 'Administrative', 'Named')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_status", "status IN ('Proposed', 'Returned', 'Rejected', 'Approved', 'Finished', 'Cancelled', 'AwaitingAccount')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_courses_instructor_kind", "instructor_kind IN ('Internal', 'ExternalAccount', 'ExternalNameOnly', 'ManagedByQuality')"));
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
        builder.Property(x => x.ArchivedByUserId).HasColumnName("archived_by");
        builder.Property(x => x.CourseNo).IsRequired().HasMaxLength(30);
        builder.Property(x => x.CurrentRevisionNo).HasDefaultValue(1);
        builder.Property(x => x.TitleAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.TitleEn).HasMaxLength(300);
        builder.Property(x => x.DeliveryMode).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ObjectivesAr).IsRequired();
        builder.Property(x => x.TargetAudienceAr).IsRequired();
        builder.Property(x => x.Origin).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.AudienceType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.InstructorKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.InstructorNameAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.InstructorNameEn).HasMaxLength(300);
        builder.Property(x => x.InstructorOrgAr).HasMaxLength(300);
        builder.Property(x => x.InstructorOrgEn).HasMaxLength(300);
        builder.Property(x => x.DeactivationCertificatesAction).HasMaxLength(20);
        builder.Property(x => x.SearchText).IsRequired().HasMaxLength(600);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne(x => x.Track).WithMany().HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProposingCenter).WithMany().HasForeignKey(x => x.ProposingCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProposedByUser).WithMany().HasForeignKey(x => x.ProposedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TargetCenter).WithMany().HasForeignKey(x => x.TargetCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EvaluationFormVersion).WithMany().HasForeignKey(x => x.EvaluationFormVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InstructorUser).WithMany().HasForeignKey(x => x.InstructorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CourseNo).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ProposingCenterId);
        builder.HasIndex(x => x.TargetCenterId);
        builder.HasIndex(x => x.InstructorUserId);
        builder.HasIndex(x => x.TrackId);
        builder.HasIndex(x => new { x.IsActive, x.IsArchived });
        builder.HasIndex(x => x.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}

