using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class MemberProfileConfiguration : IEntityTypeConfiguration<MemberProfile>
{
    public void Configure(EntityTypeBuilder<MemberProfile> builder)
    {
        builder.ToTable("member_profiles", AppDbContext.Schema);
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.ExpertiseAr).HasDefaultValueSql("'{}'::text[]");
        builder.Property(x => x.ExpertiseEn).HasDefaultValueSql("'{}'::text[]");
        builder.Property(x => x.ContactMethod).HasMaxLength(20);
        builder.Property(x => x.ContactValue).HasMaxLength(200);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PhotoFile).WithMany().HasForeignKey(x => x.PhotoFileId).OnDelete(DeleteBehavior.Restrict);
    }
}

