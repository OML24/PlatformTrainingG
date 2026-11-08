using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class EvaluationQuestionConfiguration : IEntityTypeConfiguration<EvaluationQuestion>
{
    public void Configure(EntityTypeBuilder<EvaluationQuestion> builder)
    {
        builder.ToTable("evaluation_questions", AppDbContext.Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.TextAr).IsRequired();
        builder.HasOne(x => x.Version).WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.VersionId, x.QuestionKey }).IsUnique();
    }
}

