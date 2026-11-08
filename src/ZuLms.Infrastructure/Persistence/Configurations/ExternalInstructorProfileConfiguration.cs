using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class ExternalInstructorProfileConfiguration : IEntityTypeConfiguration<ExternalInstructorProfile>
{
    public void Configure(EntityTypeBuilder<ExternalInstructorProfile> builder)
    {
        builder.ToTable("external_instructor_profiles", AppDbContext.Schema);
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.OrganizationAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.OrganizationEn).HasMaxLength(300);
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CvFile).WithMany().HasForeignKey(x => x.CvFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedFromRequest).WithMany().HasForeignKey(x => x.CreatedFromRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}

