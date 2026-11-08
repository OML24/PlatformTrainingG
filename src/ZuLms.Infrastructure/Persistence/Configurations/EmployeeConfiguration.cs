using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees", AppDbContext.Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by");
        builder.Property(x => x.UpdatedByUserId).HasColumnName("updated_by");
        builder.Property(x => x.Version).HasColumnName("xmin").IsRowVersion();
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Side).WithMany().HasForeignKey(x => x.SideCode).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.SecondName).HasMaxLength(100);
        builder.Property(x => x.ThirdName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.FullNameAr).IsRequired().HasMaxLength(400);
        builder.Property(x => x.FullNameEn).HasMaxLength(400);
        builder.HasOne(x => x.Center).WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.MainJobDesc).HasMaxLength(100);
        builder.Property(x => x.SubJobDesc).HasMaxLength(200);
        builder.Property(x => x.EmpStatusDesc).HasMaxLength(100);
        builder.Property(x => x.SourceEmail).HasMaxLength(254);
        builder.Property(x => x.SearchText).IsRequired().HasMaxLength(600);
        builder.HasIndex(x => new { x.SideCode, x.EmployeeCode }).IsUnique();
        builder.HasIndex(x => x.CenterId);
        builder.HasIndex(x => x.DepartmentId);
        builder.HasIndex(x => x.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}

