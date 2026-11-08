using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", AppDbContext.Schema);
        builder.ToTable(t => t.HasCheckConstraint("ck_users_employee_reference", "(account_type = 'Employee') = (employee_id IS NOT NULL)"));
        builder.ToTable(t => t.HasCheckConstraint("ck_users_external_login", "account_type <> 'ExternalInstructor' OR login_name ~ '^[0-9]{4}$'"));
        builder.ToTable(t => t.HasCheckConstraint("ck_users_employee_login", "account_type <> 'Employee' OR login_name ~ '^[0-9]+$'"));
        builder.ToTable(t => t.HasCheckConstraint("ck_users_account_type", "account_type IN ('Employee', 'ExternalInstructor', 'Local')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_users_credentials_email_status", "credentials_email_status IN ('NotSent', 'Sent', 'Failed', 'Uncertain')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_users_preferred_language", "preferred_language IN ('ar', 'en')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by");
        builder.Property(x => x.UpdatedByUserId).HasColumnName("updated_by");
        builder.Property(x => x.Version).HasColumnName("xmin").IsRowVersion();
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(x => x.IsActive);
        builder.Property(x => x.DeactivatedByUserId).HasColumnName("deactivated_by");
        builder.Property(x => x.AccountType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LoginName).IsRequired().HasMaxLength(20);
        builder.Property(x => x.SecurityStamp).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.FullNameAr).IsRequired().HasMaxLength(400);
        builder.Property(x => x.FullNameEn).HasMaxLength(400);
        builder.Property(x => x.EmailOverride).HasMaxLength(254);
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(300);
        builder.Property(x => x.PasswordEnc).IsRequired();
        builder.Property(x => x.CredentialsEmailStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CredentialsEmailLastError).HasMaxLength(1000);
        builder.Property(x => x.PreferredLanguage)
            .HasConversion(v => v == UiLanguage.Ar ? "ar" : "en", v => v == "ar" ? UiLanguage.Ar : UiLanguage.En)
            .HasMaxLength(2);
        builder.Property(x => x.UiSettings).HasColumnType("jsonb");
        builder.Property(x => x.SearchText).IsRequired().HasMaxLength(600);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PasswordChangedByUser).WithMany().HasForeignKey(x => x.PasswordChangedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AccessOverrideByUser).WithMany().HasForeignKey(x => x.AccessOverrideBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.LoginName).IsUnique();
        builder.HasIndex(x => x.EmployeeId).IsUnique();
        builder.HasIndex(x => x.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}

