using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class ForgotPasswordAttemptConfiguration : IEntityTypeConfiguration<ForgotPasswordAttempt>
{
    public void Configure(EntityTypeBuilder<ForgotPasswordAttempt> builder)
    {
        builder.ToTable("forgot_password_attempts", AppDbContext.Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.LoginNameInput).IsRequired().HasMaxLength(40);
        builder.Property(x => x.EmailInput).IsRequired().HasMaxLength(254);
        builder.Property(x => x.Ip).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Outcome).IsRequired().HasMaxLength(30);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LoginNameInput, x.RequestedAt });
        builder.HasIndex(x => new { x.Ip, x.RequestedAt });
    }
}

