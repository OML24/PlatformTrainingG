using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class PasswordPolicyConfiguration : IEntityTypeConfiguration<PasswordPolicy>
{
    public void Configure(EntityTypeBuilder<PasswordPolicy> builder)
    {
        builder.ToTable("password_policy", AppDbContext.Schema);
        builder.HasKey(x => x.Id);
        builder.ToTable(t => t.HasCheckConstraint("ck_password_policy_singleton", "id = 1"));
    }
}

