using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class EvaluationAnswerConfiguration : IEntityTypeConfiguration<EvaluationAnswer>
{
    public void Configure(EntityTypeBuilder<EvaluationAnswer> builder)
    {
        builder.ToTable("evaluation_answers", AppDbContext.Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.HasOne(x => x.Response).WithMany().HasForeignKey(x => x.ResponseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Option).WithMany().HasForeignKey(x => x.OptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ResponseId, x.QuestionId }).IsUnique();
    }
}

