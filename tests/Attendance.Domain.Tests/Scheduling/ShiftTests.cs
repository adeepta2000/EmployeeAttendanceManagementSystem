using Attendance.Domain.Scheduling;
using FluentAssertions;

namespace Attendance.Domain.Tests.Scheduling;

public sealed class ShiftTests
{
    [Theory]
    [InlineData(9, 0, 18, 0, false, 540)]
    [InlineData(22, 0, 6, 0, true, 480)]
    [InlineData(14, 0, 22, 0, false, 480)]
    [InlineData(16, 0, 0, 0, true, 480)] // ends exactly at midnight, on the next day
    [InlineData(0, 0, 8, 0, false, 480)]
    public void CrossesMidnight_and_duration_follow_from_the_times(
        int startHour, int startMinute, int endHour, int endMinute, bool crossesMidnight, int durationMinutes)
    {
        var shift = NewShift(new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));

        shift.CrossesMidnight.Should().Be(crossesMidnight);
        shift.DurationMinutes.Should().Be(durationMinutes);
    }

    [Fact]
    public void CrossesMidnight_is_derived_again_whenever_a_time_changes_in_any_order()
    {
        var shift = NewShift(new TimeOnly(9, 0), new TimeOnly(18, 0));

        shift.StartTime = new TimeOnly(22, 0);
        shift.CrossesMidnight.Should().BeTrue("18:00 is now earlier than the 22:00 start");

        shift.EndTime = new TimeOnly(23, 30);
        shift.CrossesMidnight.Should().BeFalse();

        shift.EndTime = new TimeOnly(6, 0);
        shift.CrossesMidnight.Should().BeTrue();
    }

    [Fact]
    public void A_night_shift_is_scheduled_on_its_start_date_and_runs_into_the_next_day_BR12()
    {
        var shift = NewShift(new TimeOnly(22, 0), new TimeOnly(6, 0));
        shift.PunchWindowBeforeMinutes = 120;
        shift.PunchWindowAfterMinutes = 240;
        var monday = new DateOnly(2026, 11, 2);

        var schedule = shift.GetSchedule(monday);

        schedule.Should().Be(new ShiftSchedule(
            ShiftDate: monday,
            Start: new DateTime(2026, 11, 2, 22, 0, 0),
            End: new DateTime(2026, 11, 3, 6, 0, 0),
            PunchWindowStart: new DateTime(2026, 11, 2, 20, 0, 0),
            PunchWindowEnd: new DateTime(2026, 11, 3, 10, 0, 0)));
    }

    [Fact]
    public void A_day_shift_is_scheduled_within_its_date()
    {
        var shift = NewShift(new TimeOnly(9, 0), new TimeOnly(18, 0));
        shift.PunchWindowBeforeMinutes = 60;
        shift.PunchWindowAfterMinutes = 180;

        var schedule = shift.GetSchedule(new DateOnly(2026, 11, 2));

        schedule.Start.Should().Be(new DateTime(2026, 11, 2, 9, 0, 0));
        schedule.End.Should().Be(new DateTime(2026, 11, 2, 18, 0, 0));
        schedule.PunchWindowStart.Should().Be(new DateTime(2026, 11, 2, 8, 0, 0));
        schedule.PunchWindowEnd.Should().Be(new DateTime(2026, 11, 2, 21, 0, 0));
    }

    private static Shift NewShift(TimeOnly start, TimeOnly end) => new()
    {
        Code = "S",
        Name = "Shift",
        StartTime = start,
        EndTime = end,
    };
}
