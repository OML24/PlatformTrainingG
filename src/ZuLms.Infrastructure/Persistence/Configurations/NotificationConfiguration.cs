using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_notifications_type", "type IN ('ProposalSubmitted', 'ProposalDecision', 'CourseApproved', 'ParticipantAdded', 'ParticipantRemoved', 'LectureCreated', 'CourseFinished', 'CertificateIssued', 'CertificateRevoked', 'EvaluationReopened', 'ImpactSubmitted', 'ImpactDecision', 'CancellationRequested', 'CancellationDecision', 'CourseCancelled', 'CourseDisabled', 'CourseRestored', 'CourseArchived', 'CourseUnarchived', 'NeedSubmitted', 'NeedDecision', 'ExtInstructorRequested', 'ExtInstructorReady', 'ProCertSubmitted', 'ProCertApproved', 'ProCertRejected', 'SyncReviewItems')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(60);
        builder.Property(x => x.TitleAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.TitleEn).HasMaxLength(300);
        builder.Property(x => x.BodyAr).IsRequired();
        builder.Property(x => x.LinkUrl).HasMaxLength(300);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(50);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
    }
}

