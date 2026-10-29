using Attendance.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations.Scheduling;

internal sealed class EmployeeShiftRosterConfiguration : IEntityTypeConfiguration<EmployeeShiftRoster>
{
    /// <summary>Spec §3.2 index on the roster date alone, for "who works on this date" lookups.</summary>
    public const string RosterDateIndex = "IX_Roster_Date";

    public void Configure(EntityTypeBuilder<EmployeeShiftRoster> builder)
    {
        builder.ToTable("EmployeeShiftRosters");

        builder.Property(r => r.Remarks).HasMaxLength(EmployeeShiftRoster.RemarksMaxLength);

        builder.HasOne(r => r.Employee)
            .WithMany()
            .HasForeignKey(r => r.EmployeeId);

        builder.HasOne(r => r.Shift)
            .WithMany()
            .HasForeignKey(r => r.ShiftId);

        // One row per employee and date, live or soft-deleted: a removed entry is restored, not duplicated.
        // The index also serves the EmployeeId foreign key.
        builder.HasIndex(r => new { r.EmployeeId, r.RosterDate }).IsUnique();

        builder.HasIndex(r => r.RosterDate).HasDatabaseName(RosterDateIndex);
    }
}
