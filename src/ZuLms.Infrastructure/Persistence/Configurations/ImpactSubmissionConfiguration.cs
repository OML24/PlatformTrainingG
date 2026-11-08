using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class ImpactSubmissionConfiguration : IEntityTypeConfiguration<ImpactSubmission>
{
    public void Configure(EntityTypeBuilder<ImpactSubmission> builder)
    {
        builder.ToTable("impact_submissions", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_impact_submissions_impact_type", "impact_type IN ('StudyPlan', 'ExamModel', 'ClassActivity')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_impact_submissions_status", "status IN ('Submitted', 'Returned', 'Approved')"));
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
        builder.Property(x => x.ImpactType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FileSha256).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne(x => x.Participant).WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DecidedByUser).WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ParticipantId, x.SequenceNo }).IsUnique();
    }
}

