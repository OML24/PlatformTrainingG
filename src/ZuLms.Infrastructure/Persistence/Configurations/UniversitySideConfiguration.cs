using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class UniversitySideConfiguration : IEntityTypeConfiguration<UniversitySide>
{
    public void Configure(EntityTypeBuilder<UniversitySide> builder)
    {
        builder.ToTable("university_sides", AppDbContext.Schema);
        builder.HasKey(x => x.SideCode);
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
    }
}

