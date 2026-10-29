using Attendance.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations.Scheduling;

internal sealed class WeeklyOffPatternConfiguration : IEntityTypeConfiguration<WeeklyOffPattern>
{
    public void Configure(EntityTypeBuilder<WeeklyOffPattern> builder)
    {
        builder.ToTable("WeeklyOffPatterns");

        builder.Property(p => p.Name).HasMaxLength(WeeklyOffPattern.NameMaxLength);
        builder.HasIndex(p => p.Name).IsUnique();

        builder.HasMany(p => p.Days)
            .WithOne()
            .HasForeignKey(d => d.PatternId);
    }
}

internal sealed class WeeklyOffPatternDayConfiguration : IEntityTypeConfiguration<WeeklyOffPatternDay>
{
    public void Configure(EntityTypeBuilder<WeeklyOffPatternDay> builder)
    {
        builder.ToTable("WeeklyOffPatternDays", table =>
        {
            table.HasCheckConstraint("CK_WeeklyOffPatternDays_DayOfWeek", "[DayOfWeek] BETWEEN 0 AND 6");
            table.HasCheckConstraint("CK_WeeklyOffPatternDays_WeekOfMonth", $"[WeekOfMonth] BETWEEN 1 AND {WeeklyOffPattern.MaxWeekOfMonth}");
        });

        // TINYINT (spec §3.2), as System.DayOfWeek numbers it: Sunday = 0 to Saturday = 6.
        builder.Property(d => d.DayOfWeek).HasConversion<byte>();

        // HasFilter(null) drops the IS NOT NULL filter EF adds to unique indexes on nullable columns, so
        // "every Friday" (a null WeekOfMonth) can't be listed twice either.
        builder.HasIndex(d => new { d.PatternId, d.DayOfWeek, d.WeekOfMonth })
            .IsUnique()
            .HasFilter(null);
    }
}
