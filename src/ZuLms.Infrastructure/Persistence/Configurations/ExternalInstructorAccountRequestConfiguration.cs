using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class ExternalInstructorAccountRequestConfiguration : IEntityTypeConfiguration<ExternalInstructorAccountRequest>
{
    public void Configure(EntityTypeBuilder<ExternalInstructorAccountRequest> builder)
    {
        builder.ToTable("external_instructor_account_requests", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_external_instructor_account_requests_status", "status IN ('PendingAdmin', 'Completed', 'Rejected')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by");
        builder.Property(x => x.UpdatedByUserId).HasColumnName("updated_by");
        builder.Property(x => x.Version).HasColumnName("xmin").IsRowVersion();
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.FullNameAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.FullNameEn).HasMaxLength(300);
        builder.Property(x => x.OrganizationAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.OrganizationEn).HasMaxLength(300);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(254);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequestedByUser).WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ForwardedByUser).WithMany().HasForeignKey(x => x.ForwardedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.HandledByUser).WithMany().HasForeignKey(x => x.HandledBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedUser).WithMany().HasForeignKey(x => x.CreatedUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

