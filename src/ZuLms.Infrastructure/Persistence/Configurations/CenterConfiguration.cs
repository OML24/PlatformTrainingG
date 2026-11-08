using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class CenterConfiguration : IEntityTypeConfiguration<Center>
{
    public void Configure(EntityTypeBuilder<Center> builder)
    {
        builder.ToTable("centers", AppDbContext.Schema);
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
        builder.HasOne(x => x.Side).WithMany().HasForeignKey(x => x.SideCode).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.NameEn).HasMaxLength(300);
        builder.Property(x => x.ShortCode).HasMaxLength(10);
        builder.Property(x => x.SourceHash).HasMaxLength(64);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasIndex(x => new { x.SideCode, x.CenterCode }).IsUnique();
        builder.HasIndex(x => x.IsFaculty).HasFilter("is_active");
    }
}

