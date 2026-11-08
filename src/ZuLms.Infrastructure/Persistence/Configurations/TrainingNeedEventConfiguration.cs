using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class TrainingNeedEventConfiguration : IEntityTypeConfiguration<TrainingNeedEvent>
{
    public void Configure(EntityTypeBuilder<TrainingNeedEvent> builder)
    {
        builder.ToTable("training_need_events", AppDbContext.Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Event).IsRequired().HasMaxLength(30);
        builder.HasOne(x => x.Need).WithMany().HasForeignKey(x => x.NeedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ByUser).WithMany().HasForeignKey(x => x.ByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

