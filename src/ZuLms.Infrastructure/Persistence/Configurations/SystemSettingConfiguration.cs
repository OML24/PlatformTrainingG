using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("system_settings", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_system_settings_value_type", "value_type IN ('Int', 'Decimal', 'Bool', 'String', 'Json')"));
        builder.HasKey(x => x.SettingKey);
        builder.Property(x => x.Version).HasColumnName("xmin").IsRowVersion();
        builder.Property(x => x.SettingKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.GroupKey).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ValueType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Value).IsRequired();
        builder.Property(x => x.DefaultValue).IsRequired();
        builder.Property(x => x.LabelAr).IsRequired().HasMaxLength(200);
        builder.Property(x => x.LabelEn).HasMaxLength(200);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

