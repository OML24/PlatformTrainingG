using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutbox>
{
    public void Configure(EntityTypeBuilder<EmailOutbox> builder)
    {
        builder.ToTable("email_outbox", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_email_outbox_kind", "kind IN ('InitialCredentials', 'ForgotPassword', 'ExternalInstructorAccount')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_email_outbox_status", "status IN ('Pending', 'Sent', 'Failed', 'Uncertain')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.ToAddress).IsRequired().HasMaxLength(254);
        builder.Property(x => x.ToName).HasMaxLength(300);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(300);
        builder.Property(x => x.BodyHtml).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.Property(x => x.ProviderMessageId).HasMaxLength(200);
        builder.HasOne(x => x.RelatedUser).WithMany().HasForeignKey(x => x.RelatedUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
}

