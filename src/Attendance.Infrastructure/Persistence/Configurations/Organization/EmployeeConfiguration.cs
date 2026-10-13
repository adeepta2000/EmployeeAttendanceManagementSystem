using Attendance.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations.Organization;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.Property(e => e.EmployeeCode).HasMaxLength(Employee.EmployeeCodeMaxLength);
        builder.HasIndex(e => e.EmployeeCode).IsUnique();

        builder.Property(e => e.FirstName).HasMaxLength(Employee.NameMaxLength);
        builder.Property(e => e.LastName).HasMaxLength(Employee.NameMaxLength);
        builder.Property(e => e.Email).HasMaxLength(Employee.EmailMaxLength);
        builder.Property(e => e.Phone).HasMaxLength(Employee.PhoneMaxLength);
        builder.Property(e => e.PhotoPath).HasMaxLength(Employee.PhotoPathMaxLength);

        builder.HasOne(e => e.Department)
            .WithMany()
            .HasForeignKey(e => e.DepartmentId);

        builder.HasOne(e => e.Designation)
            .WithMany()
            .HasForeignKey(e => e.DesignationId);

        builder.HasOne(e => e.WorkLocation)
            .WithMany()
            .HasForeignKey(e => e.WorkLocationId);

        builder.HasOne(e => e.ReportingManager)
            .WithMany()
            .HasForeignKey(e => e.ReportingManagerId);

        // Spec §3.2 indexes. The composite one also serves the DepartmentId foreign key.
        builder.HasIndex(e => new { e.DepartmentId, e.IsActive })
            .HasDatabaseName("IX_Employees_DepartmentId_IsActive");

        builder.HasIndex(e => e.ReportingManagerId)
            .HasDatabaseName("IX_Employees_ReportingManagerId");
    }
}
