using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class ProfessionalCertificationConfiguration : IEntityTypeConfiguration<ProfessionalCertification>
{
    public void Configure(EntityTypeBuilder<ProfessionalCertification> builder)
    {
        builder.ToTable("professional_certifications", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_procert_evidence", "credential_code IS NOT NULL OR verify_url IS NOT NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_procert_expiry", "expiry_date IS NULL OR expiry_date >= issued_date"));
        builder.ToTable(t => t.HasCheckConstraint("ck_procert_verify_url", "verify_url IS NULL OR verify_url ~* '^https?://'"));
        builder.ToTable(t => t.HasCheckConstraint("ck_professional_certifications_status", "status IN ('Submitted', 'Approved', 'Rejected')"));
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
        builder.Property(x => x.Name).IsRequired().HasMaxLength(300);
        builder.Property(x => x.NameAr).HasMaxLength(300);
        builder.Property(x => x.IssuerName).IsRequired().HasMaxLength(300);
        builder.Property(x => x.IssuerWebsiteUrl).HasMaxLength(600);
        builder.Property(x => x.CredentialCode).HasMaxLength(200);
        builder.Property(x => x.VerifyUrl).HasMaxLength(600);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SearchText).IsRequired().HasMaxLength(900);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DecidedByUser).WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.Status });
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => new { x.Status, x.SubmittedAt });
        builder.HasIndex(x => x.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}

