using Attendance.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations.Organization;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");

        builder.Property(d => d.Code).HasMaxLength(Department.CodeMaxLength);
        builder.HasIndex(d => d.Code).IsUnique();

        builder.Property(d => d.Name).HasMaxLength(Department.NameMaxLength);

        builder.HasOne(d => d.ParentDepartment)
            .WithMany()
            .HasForeignKey(d => d.ParentDepartmentId);

        builder.HasOne(d => d.HeadEmployee)
            .WithMany()
            .HasForeignKey(d => d.HeadEmployeeId);
    }
}
