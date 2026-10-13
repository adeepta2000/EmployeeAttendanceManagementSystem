using Attendance.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations.Identity;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.Property(p => p.Code).HasMaxLength(Permission.CodeMaxLength);
        builder.HasIndex(p => p.Code).IsUnique();

        builder.Property(p => p.Module).HasMaxLength(Permission.ModuleMaxLength);
        builder.Property(p => p.Description).HasMaxLength(Permission.DescriptionMaxLength);
    }
}
