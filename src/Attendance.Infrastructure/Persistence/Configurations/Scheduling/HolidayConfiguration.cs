using Attendance.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations.Scheduling;

internal sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("Holidays");

        builder.Property(h => h.Name).HasMaxLength(Holiday.NameMaxLength);
        builder.Property(h => h.Description).HasMaxLength(Holiday.DescriptionMaxLength);

        // The same holiday can't be entered twice on a date. Two different ones can share a date, e.g. a
        // Dhaka-only and a Chittagong-only holiday. Removed holidays don't count.
        builder.HasIndex(h => new { h.HolidayDate, h.Name })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasMany(h => h.Locations)
            .WithOne(l => l.Holiday)
            .HasForeignKey(l => l.HolidayId);
    }
}

internal sealed class HolidayLocationConfiguration : IEntityTypeConfiguration<HolidayLocation>
{
    public void Configure(EntityTypeBuilder<HolidayLocation> builder)
    {
        builder.ToTable("HolidayLocations");

        builder.HasKey(l => new { l.HolidayId, l.WorkLocationId });

        builder.HasOne(l => l.WorkLocation)
            .WithMany()
            .HasForeignKey(l => l.WorkLocationId);
    }
}
