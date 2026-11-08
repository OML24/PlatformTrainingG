using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class BlockedPasswordConfiguration : IEntityTypeConfiguration<BlockedPassword>
{
    public void Configure(EntityTypeBuilder<BlockedPassword> builder)
    {
        builder.ToTable("blocked_passwords", AppDbContext.Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.PasswordNormalized).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(20);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PasswordNormalized).IsUnique();
    }
}

