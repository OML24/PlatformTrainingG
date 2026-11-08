using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class SyncReviewItemConfiguration : IEntityTypeConfiguration<SyncReviewItem>
{
    public void Configure(EntityTypeBuilder<SyncReviewItem> builder)
    {
        builder.ToTable("sync_review_items", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_sync_review_items_reason", "reason IN ('MissingFromSource', 'UnresolvedReference', 'DuplicateLoginName', 'DuplicateSourceKey', 'SharedEmail', 'StatusChanged')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_sync_review_items_resolution", "resolution IN ('Keep', 'Disable', 'Ignore')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_sync_review_items_origin", "origin IN ('Sync', 'ForgotPassword', 'Provisioning')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(30);
        builder.Property(x => x.EntityKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Reason).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Origin).HasConversion<string>().HasMaxLength(20).HasDefaultValue(SyncReviewOrigin.Sync);
        builder.Property(x => x.Resolution).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.FirstSeenRun).WithMany().HasForeignKey(x => x.FirstSeenRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResolvedByUser).WithMany().HasForeignKey(x => x.ResolvedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.EntityType, x.EntityKey, x.Reason }).IsUnique().HasFilter("resolved_at IS NULL");
    }
}

