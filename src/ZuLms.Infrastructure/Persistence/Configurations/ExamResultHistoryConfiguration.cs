using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class ExamResultHistoryConfiguration : IEntityTypeConfiguration<ExamResultHistory>
{
    public void Configure(EntityTypeBuilder<ExamResultHistory> builder)
    {
        builder.ToTable("exam_result_history", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_exam_result_history_old_outcome", "old_outcome IN ('Passed', 'Failed')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_exam_result_history_new_outcome", "new_outcome IN ('Passed', 'Failed')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.OldOutcome).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.NewOutcome).HasConversion<string>().HasMaxLength(10);
        builder.HasOne(x => x.ExamResult).WithMany().HasForeignKey(x => x.ExamResultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

