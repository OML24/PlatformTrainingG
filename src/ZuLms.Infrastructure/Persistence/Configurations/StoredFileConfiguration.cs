using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("stored_files", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_stored_files_kind", "kind IN ('ProposalCouncilLetter', 'InstructorCv', 'CancellationLetter', 'NeedLetter', 'TrainingMaterial', 'ImpactFile', 'CertificateTemplate', 'CertificatePdf', 'ProfilePhoto', 'ProfessionalCertificate', 'QualityMinutes', 'Recommendation', 'Other')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.OriginalName).IsRequired().HasMaxLength(300);
        builder.Property(x => x.StoredName).IsRequired().HasMaxLength(80);
        builder.Property(x => x.RelativePath).IsRequired().HasMaxLength(500);
        builder.Property(x => x.MimeType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Sha256).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.StoredName).IsUnique();
        builder.HasIndex(x => x.Sha256);
    }
}

