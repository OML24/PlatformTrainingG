using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class VaultDocumentConfiguration : IEntityTypeConfiguration<VaultDocument>
{
    public void Configure(EntityTypeBuilder<VaultDocument> builder)
    {
        builder.ToTable("vault_documents", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_vault_documents_doc_type", "doc_type IN ('ProposalCouncilLetter', 'InstructorCv', 'CancellationLetter', 'NeedLetter', 'TrainingMaterial', 'ImpactFile', 'CertificateTemplate', 'CertificatePdf', 'ProfilePhoto', 'ProfessionalCertificate', 'QualityMinutes', 'Recommendation', 'Other')"));
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
        builder.Property(x => x.DocType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.TitleAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.TitleEn).HasMaxLength(300);
        builder.Property(x => x.SourceEntityType).HasMaxLength(50);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RelatedCourse).WithMany().HasForeignKey(x => x.RelatedCourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RelatedCenter).WithMany().HasForeignKey(x => x.RelatedCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.DocType);
        builder.HasIndex(x => x.RelatedCourseId);
        builder.HasIndex(x => x.RelatedCenterId);
    }
}

