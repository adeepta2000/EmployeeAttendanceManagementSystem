using Attendance.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations.Scheduling;

internal sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        // CrossesMidnight is derived in the entity; the database refuses a row where it disagrees with the
        // times, whoever wrote it.
        builder.ToTable("Shifts", table =>
        {
            table.HasCheckConstraint("CK_Shifts_StartTime_EndTime", "[StartTime] <> [EndTime]");
            table.HasCheckConstraint("CK_Shifts_CrossesMidnight", "[CrossesMidnight] = CASE WHEN [EndTime] < [StartTime] THEN 1 ELSE 0 END");
        });

        builder.Property(s => s.Code).HasMaxLength(Shift.CodeMaxLength);
        builder.HasIndex(s => s.Code).IsUnique();

        builder.Property(s => s.Name).HasMaxLength(Shift.NameMaxLength);
        builder.Property(s => s.StartTime).HasColumnType("time(0)");
        builder.Property(s => s.EndTime).HasColumnType("time(0)");
    }
}
