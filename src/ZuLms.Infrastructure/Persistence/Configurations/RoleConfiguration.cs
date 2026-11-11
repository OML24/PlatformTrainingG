using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_roles_code", "code IN ('Admin', 'Quality', 'Dean', 'Instructor', 'Member', 'ExternalInstructor', 'CenterManager')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Code).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.NameAr).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

