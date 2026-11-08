using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class CourseParticipantConfiguration : IEntityTypeConfiguration<CourseParticipant>
{
    public void Configure(EntityTypeBuilder<CourseParticipant> builder)
    {
        builder.ToTable("course_participants", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_course_participants_source", "source IN ('SelfRegistered', 'AddedByQuality', 'AddedByAdmin', 'HistoricalEntry')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by");
        builder.Property(x => x.UpdatedByUserId).HasColumnName("updated_by");
        builder.Property(x => x.Version).HasColumnName("xmin").IsRowVersion();
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.AdminCertificateOverride).HasDefaultValue(false);
        builder.Property(x => x.EvaluationExempt).HasDefaultValue(false);
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AddedByUser).WithMany().HasForeignKey(x => x.AddedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CourseId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

