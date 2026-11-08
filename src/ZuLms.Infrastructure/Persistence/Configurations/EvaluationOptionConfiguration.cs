using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class EvaluationOptionConfiguration : IEntityTypeConfiguration<EvaluationOption>
{
    public void Configure(EntityTypeBuilder<EvaluationOption> builder)
    {
        builder.ToTable("evaluation_options", AppDbContext.Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.LabelAr).IsRequired().HasMaxLength(300);
        builder.Property(x => x.LabelEn).HasMaxLength(300);
        builder.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}

