using Attendance.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations.Organization;

internal sealed class DesignationConfiguration : IEntityTypeConfiguration<Designation>
{
    public void Configure(EntityTypeBuilder<Designation> builder)
    {
        builder.ToTable("Designations");

        builder.Property(d => d.Code).HasMaxLength(Designation.CodeMaxLength);
        builder.HasIndex(d => d.Code).IsUnique();

        builder.Property(d => d.Name).HasMaxLength(Designation.NameMaxLength);
        builder.Property(d => d.Grade).HasMaxLength(Designation.GradeMaxLength);
    }
}
