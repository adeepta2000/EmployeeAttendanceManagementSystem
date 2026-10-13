using Attendance.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations.Organization;

internal sealed class WorkLocationConfiguration : IEntityTypeConfiguration<WorkLocation>
{
    public void Configure(EntityTypeBuilder<WorkLocation> builder)
    {
        builder.ToTable("WorkLocations");

        builder.Property(w => w.Code).HasMaxLength(WorkLocation.CodeMaxLength);
        builder.HasIndex(w => w.Code).IsUnique();

        builder.Property(w => w.Name).HasMaxLength(WorkLocation.NameMaxLength);
        builder.Property(w => w.Address).HasMaxLength(WorkLocation.AddressMaxLength);

        builder.Property(w => w.Latitude).HasPrecision(9, 6);
        builder.Property(w => w.Longitude).HasPrecision(9, 6);
    }
}
