using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_permissions_kind", "kind IN ('PageView', 'Action')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_permissions_scope", "scope IN ('Global', 'OwnFaculty', 'InstructedCourse', 'Self')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.PermKey).IsRequired().HasMaxLength(120);
        builder.Property(x => x.GroupKey).IsRequired().HasMaxLength(60);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(x => x.NameEn).HasMaxLength(200);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasIndex(x => x.PermKey).IsUnique();
    }
}

