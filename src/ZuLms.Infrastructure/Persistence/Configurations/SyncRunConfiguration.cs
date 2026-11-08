using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.ToTable("sync_runs", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_sync_runs_status", "status IN ('Success', 'Partial', 'Failed')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.StatsJson).IsRequired().HasColumnType("jsonb");
        builder.HasOne(x => x.TriggeredByUser).WithMany().HasForeignKey(x => x.TriggeredBy).OnDelete(DeleteBehavior.Restrict);
    }
}

