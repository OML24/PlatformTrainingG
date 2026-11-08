using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class ProfessionalCertEventConfiguration : IEntityTypeConfiguration<ProfessionalCertEvent>
{
    public void Configure(EntityTypeBuilder<ProfessionalCertEvent> builder)
    {
        builder.ToTable("professional_cert_events", AppDbContext.Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Event).IsRequired().HasMaxLength(20);
        builder.HasOne(x => x.Cert).WithMany().HasForeignKey(x => x.CertId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ByUser).WithMany().HasForeignKey(x => x.ByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

