using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class TrainingNeedConfiguration : IEntityTypeConfiguration<TrainingNeed>
{
    public void Configure(EntityTypeBuilder<TrainingNeed> builder)
    {
        builder.ToTable("training_needs", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_training_needs_status", "status IN ('Submitted', 'Returned', 'Approved')"));
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
        builder.Property(x => x.TitleAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.CurrentRevision).HasDefaultValue(1);
        builder.Property(x => x.TitleEn).HasMaxLength(300);
        builder.Property(x => x.DescriptionAr).IsRequired();
        builder.Property(x => x.PreferredTimeframeAr).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne(x => x.Center).WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubmittedByUser).WithMany().HasForeignKey(x => x.SubmittedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Track).WithMany().HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequestedInstructorUser).WithMany().HasForeignKey(x => x.RequestedInstructorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LetterFile).WithMany().HasForeignKey(x => x.LetterFileId).OnDelete(DeleteBehavior.Restrict);
    }
}

