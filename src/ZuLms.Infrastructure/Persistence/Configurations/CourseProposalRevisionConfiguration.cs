using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class CourseProposalRevisionConfiguration : IEntityTypeConfiguration<CourseProposalRevision>
{
    public void Configure(EntityTypeBuilder<CourseProposalRevision> builder)
    {
        builder.ToTable("course_proposal_revisions", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_course_proposal_revisions_decision", "decision IN ('Pending', 'Returned', 'Approved', 'Rejected')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.SnapshotJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubmittedByUser).WithMany().HasForeignKey(x => x.SubmittedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DecidedByUser).WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CourseId, x.RevisionNo }).IsUnique();
    }
}

