using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class EvaluationFormVersionConfiguration : IEntityTypeConfiguration<EvaluationFormVersion>
{
    public void Configure(EntityTypeBuilder<EvaluationFormVersion> builder)
    {
        builder.ToTable("evaluation_form_versions", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_evaluation_form_versions_status", "status IN ('Draft', 'Published')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by");
        builder.Property(x => x.UpdatedByUserId).HasColumnName("updated_by");
        builder.Property(x => x.Version).HasColumnName("xmin").IsRowVersion();
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TitleAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.TitleEn).HasMaxLength(300);
        builder.HasOne(x => x.PublishedByUser).WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.VersionNo).IsUnique();
        builder.HasIndex(x => x.Status).IsUnique().HasFilter("status = 'Draft'");
    }
}

