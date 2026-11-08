using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class CourseCancellationRequestConfiguration : IEntityTypeConfiguration<CourseCancellationRequest>
{
    public void Configure(EntityTypeBuilder<CourseCancellationRequest> builder)
    {
        builder.ToTable("course_cancellation_requests", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_course_cancellation_requests_status", "status IN ('Pending', 'Approved', 'Rejected')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by");
        builder.Property(x => x.UpdatedByUserId).HasColumnName("updated_by");
        builder.Property(x => x.Version).HasColumnName("xmin").IsRowVersion();
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Reason).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequestedByUser).WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LetterFile).WithMany().HasForeignKey(x => x.LetterFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DecidedByUser).WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CourseId).IsUnique().HasFilter("status = 'Pending'");
    }
}

