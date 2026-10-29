using Attendance.Domain.Scheduling;
using FluentAssertions;

namespace Attendance.Domain.Tests.Scheduling;

/// <summary>
/// The calendar context of the processing engine (spec §2.7): day type and effective shift per
/// employee and date.
/// </summary>
/// <remarks>
/// August 2026 starts on a Saturday, so it has five Saturdays (1, 8, 15, 22, 29) and four Fridays
/// (7, 14, 21, 28): a good month for the alternate-Saturday rule, whose 5th Saturday is a working day.
/// </remarks>
public sealed class CalendarServiceTests
{
    private const long Rafiq = 1;
    private const long Nusrat = 2;
    private const long Dhaka = 10;
    private const long Chittagong = 20;

    private static readonly DateOnly From = new(2026, 8, 1);
    private static readonly DateOnly To = new(2026, 11, 30);

    private static readonly Shift General = NewShift("GEN", new TimeOnly(9, 0), new TimeOnly(18, 0));
    private static readonly Shift Night = NewShift("NIGHT", new TimeOnly(22, 0), new TimeOnly(6, 0));

    private static readonly WeeklyOffPattern EveryFriday = new("Friday", [new WeeklyOffDay(DayOfWeek.Friday)]);

    private static readonly WeeklyOffPattern FridayAndAlternateSaturdays = new(
        "Friday + alternate Saturdays",
        [new WeeklyOffDay(DayOfWeek.Friday), new WeeklyOffDay(DayOfWeek.Saturday, 2), new WeeklyOffDay(DayOfWeek.Saturday, 4)]);

    [Fact]
    public void With_no_roster_entry_the_default_shift_applies()
    {
        var calendar = Calendar(employees: [Profile(Rafiq, defaultShift: General)]);

        calendar.GetEffectiveShift(Rafiq, new DateOnly(2026, 8, 3)).Should().BeSameAs(General);
        calendar.GetDayType(Rafiq, new DateOnly(2026, 8, 3)).Should().Be(DayType.Working);
        calendar.GetDay(Rafiq, new DateOnly(2026, 8, 3)).Roster.Should().BeNull();
    }

    [Fact]
    public void A_roster_entry_overrides_the_default_shift_on_its_date_only()
    {
        var monday = new DateOnly(2026, 8, 3);
        var calendar = Calendar(
            employees: [Profile(Rafiq, defaultShift: General)],
            rosters: [EmployeeShiftRoster.Create(Rafiq, monday, Night, isWeeklyOffOverride: null, remarks: null)]);

        calendar.GetEffectiveShift(Rafiq, monday).Should().BeSameAs(Night);
        calendar.GetEffectiveShift(Rafiq, monday.AddDays(1)).Should().BeSameAs(General);
        calendar.GetEffectiveShift(Rafiq, monday.AddDays(-1)).Should().BeSameAs(General);
    }

    [Fact]
    public void A_roster_entry_gives_a_shift_to_an_employee_without_a_default()
    {
        var monday = new DateOnly(2026, 8, 3);
        var calendar = Calendar(
            employees: [Profile(Rafiq, defaultShift: null)],
            rosters: [EmployeeShiftRoster.Create(Rafiq, monday, Night, isWeeklyOffOverride: null, remarks: null)]);

        calendar.GetEffectiveShift(Rafiq, monday).Should().BeSameAs(Night);
        calendar.GetEffectiveShift(Rafiq, monday.AddDays(1)).Should().BeNull("there is neither a roster entry nor a default shift");
    }

    [Fact]
    public void A_removed_roster_entry_no_longer_overrides_anything()
    {
        var monday = new DateOnly(2026, 8, 3);
        var removed = EmployeeShiftRoster.Create(Rafiq, monday, Night, isWeeklyOffOverride: true, remarks: null);
        removed.Remove();
        var calendar = Calendar(employees: [Profile(Rafiq, defaultShift: General)], rosters: [removed]);

        calendar.GetEffectiveShift(Rafiq, monday).Should().BeSameAs(General);
        calendar.GetDayType(Rafiq, monday).Should().Be(DayType.Working);
    }

    [Fact]
    public void Every_Friday_is_a_weekly_off_and_no_other_day_is()
    {
        var calendar = Calendar(employees: [Profile(Rafiq, pattern: EveryFriday)]);

        WeeklyOffsIn(calendar, Rafiq, 2026, 8).Should().Equal(Dates(2026, 8, 7, 14, 21, 28));
    }

    [Fact]
    public void Alternate_Saturdays_are_the_2nd_and_4th_of_the_month_and_the_rest_are_working_days()
    {
        var calendar = Calendar(employees: [Profile(Rafiq, pattern: FridayAndAlternateSaturdays)]);

        // Every Friday, plus Saturdays 8 and 22. Saturdays 1, 15 and 29 (the 1st, 3rd and 5th) are working days.
        WeeklyOffsIn(calendar, Rafiq, 2026, 8).Should().Equal(Dates(2026, 8, 7, 8, 14, 21, 22, 28));
        foreach (var workingSaturday in Dates(2026, 8, 1, 15, 29))
        {
            calendar.GetDayType(Rafiq, workingSaturday).Should().Be(DayType.Working, $"{workingSaturday} is not the 2nd or 4th Saturday");
        }
    }

    [Fact]
    public void Alternate_Saturdays_follow_each_months_own_calendar()
    {
        var calendar = Calendar(employees: [Profile(Rafiq, pattern: FridayAndAlternateSaturdays)]);

        // October 2026 starts on a Thursday: its Saturdays are 3, 10, 17, 24 and 31.
        WeeklyOffsIn(calendar, Rafiq, 2026, 10).Should().Equal(Dates(2026, 10, 2, 9, 10, 16, 23, 24, 30));
    }

    [Fact]
    public void An_employee_without_a_pattern_has_no_regular_weekly_off()
    {
        var calendar = Calendar(employees: [Profile(Rafiq, pattern: null)]);

        WeeklyOffsIn(calendar, Rafiq, 2026, 8).Should().BeEmpty();
    }

    [Fact]
    public void A_holiday_limited_to_Dhaka_is_a_holiday_in_Dhaka_but_a_working_day_in_Chittagong()
    {
        var tuesday = new DateOnly(2026, 11, 3);
        var calendar = Calendar(
            employees: [Profile(Rafiq, Dhaka, General), Profile(Nusrat, Chittagong, General)],
            holidays: [NewHoliday("Dhaka City Day", tuesday, Dhaka)]);

        calendar.GetDayType(Rafiq, tuesday).Should().Be(DayType.Holiday);
        calendar.GetDay(Rafiq, tuesday).Holiday!.Name.Should().Be("Dhaka City Day");
        calendar.GetDayType(Nusrat, tuesday).Should().Be(DayType.Working);
        calendar.GetDay(Nusrat, tuesday).Holiday.Should().BeNull();
    }

    [Fact]
    public void A_holiday_with_no_locations_applies_everywhere()
    {
        var wednesday = new DateOnly(2026, 11, 4);
        var calendar = Calendar(
            employees: [Profile(Rafiq, Dhaka, General), Profile(Nusrat, Chittagong, General)],
            holidays: [NewHoliday("Foundation Day", wednesday)]);

        calendar.GetDayType(Rafiq, wednesday).Should().Be(DayType.Holiday);
        calendar.GetDayType(Nusrat, wednesday).Should().Be(DayType.Holiday);
    }

    [Fact]
    public void A_holiday_falling_on_a_weekly_off_is_a_holiday()
    {
        var friday = new DateOnly(2026, 8, 14);
        var calendar = Calendar(
            employees: [Profile(Rafiq, Dhaka, General, FridayAndAlternateSaturdays)],
            holidays: [NewHoliday("Foundation Day", friday)]);

        var day = calendar.GetDay(Rafiq, friday);

        day.DayType.Should().Be(DayType.Holiday, "a holiday outranks a weekly off, and carries the higher OT multiplier");
        day.Holiday!.Name.Should().Be("Foundation Day");
        day.Shift.Should().BeSameAs(General, "the shift still frames the punch window if someone works that day");
    }

    [Fact]
    public void A_holiday_outranks_a_roster_entry_that_makes_the_day_a_working_day()
    {
        var friday = new DateOnly(2026, 8, 14);
        var calendar = Calendar(
            employees: [Profile(Rafiq, Dhaka, General, EveryFriday)],
            rosters: [EmployeeShiftRoster.Create(Rafiq, friday, Night, isWeeklyOffOverride: false, remarks: "Shipment")],
            holidays: [NewHoliday("Foundation Day", friday)]);

        calendar.GetDayType(Rafiq, friday).Should().Be(DayType.Holiday);
        calendar.GetEffectiveShift(Rafiq, friday).Should().BeSameAs(Night);
    }

    [Fact]
    public void Optional_and_removed_holidays_do_not_make_a_holiday()
    {
        var tuesday = new DateOnly(2026, 11, 3);
        var wednesday = new DateOnly(2026, 11, 4);
        var optional = NewHoliday("Optional Day", tuesday);
        optional.IsOptional = true;
        var removed = NewHoliday("Cancelled Day", wednesday);
        removed.Remove();
        var calendar = Calendar(employees: [Profile(Rafiq, Dhaka, General)], holidays: [optional, removed]);

        calendar.GetDayType(Rafiq, tuesday).Should().Be(DayType.Working);
        calendar.GetDayType(Rafiq, wednesday).Should().Be(DayType.Working);
    }

    [Theory]
    [InlineData("2026-08-08", true, DayType.WeeklyOff)] // a pattern weekly off stays one
    [InlineData("2026-08-08", false, DayType.Working)] // a pattern weekly off swapped for a working day
    [InlineData("2026-08-10", true, DayType.WeeklyOff)] // a working Monday rostered off
    [InlineData("2026-08-10", false, DayType.Working)]
    public void A_roster_weekly_off_override_replaces_the_patterns_verdict_in_either_direction(
        string date, bool isWeeklyOffOverride, DayType expected)
    {
        var rosterDate = DateOnly.Parse(date);
        var calendar = Calendar(
            employees: [Profile(Rafiq, defaultShift: General, pattern: FridayAndAlternateSaturdays)],
            rosters: [EmployeeShiftRoster.Create(Rafiq, rosterDate, General, isWeeklyOffOverride, remarks: null)]);

        calendar.GetDayType(Rafiq, rosterDate).Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-08-08", DayType.WeeklyOff)] // the 2nd Saturday
    [InlineData("2026-08-10", DayType.Working)]
    public void A_roster_entry_without_an_override_leaves_the_weekly_off_to_the_pattern(string date, DayType expected)
    {
        var rosterDate = DateOnly.Parse(date);
        var calendar = Calendar(
            employees: [Profile(Rafiq, defaultShift: General, pattern: FridayAndAlternateSaturdays)],
            rosters: [EmployeeShiftRoster.Create(Rafiq, rosterDate, Night, isWeeklyOffOverride: null, remarks: null)]);

        calendar.GetDayType(Rafiq, rosterDate).Should().Be(expected);
        calendar.GetEffectiveShift(Rafiq, rosterDate).Should().BeSameAs(Night);
    }

    [Fact]
    public void A_night_shift_belongs_entirely_to_the_date_it_starts_on_BR12()
    {
        var monday = new DateOnly(2026, 11, 2);
        var tuesday = monday.AddDays(1);
        var calendar = Calendar(
            employees: [Profile(Rafiq, Dhaka, Night)],
            holidays: [NewHoliday("Dhaka City Day", tuesday, Dhaka)]);

        var mondayShift = calendar.GetDay(Rafiq, monday);
        var schedule = mondayShift.Shift!.GetSchedule(monday);

        // 22:00 Monday to 06:00 Tuesday is Monday's shift, worked on a working day, although it ends on a holiday.
        mondayShift.DayType.Should().Be(DayType.Working);
        schedule.Start.Should().Be(new DateTime(2026, 11, 2, 22, 0, 0));
        schedule.End.Should().Be(new DateTime(2026, 11, 3, 6, 0, 0));
        schedule.ShiftDate.Should().Be(monday);
        calendar.GetDayType(Rafiq, tuesday).Should().Be(DayType.Holiday, "Tuesday's own shift, starting at 22:00, is the holiday one");
    }

    [Fact]
    public void A_date_outside_the_loaded_range_is_refused_rather_than_guessed()
    {
        var calendar = Calendar(employees: [Profile(Rafiq, defaultShift: General)]);

        var before = () => calendar.GetDayType(Rafiq, From.AddDays(-1));
        var after = () => calendar.GetEffectiveShift(Rafiq, To.AddDays(1));

        before.Should().Throw<ArgumentOutOfRangeException>();
        after.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void An_employee_that_was_not_loaded_is_refused()
    {
        var calendar = Calendar(employees: [Profile(Rafiq, defaultShift: General)]);

        var act = () => calendar.GetDayType(Nusrat, From);

        act.Should().Throw<ArgumentException>().WithParameterName("employeeId");
    }

    private static CalendarService Calendar(
        IReadOnlyCollection<EmployeeCalendarProfile> employees,
        IReadOnlyCollection<EmployeeShiftRoster>? rosters = null,
        IReadOnlyCollection<Holiday>? holidays = null) =>
        new(new CalendarData(From, To, employees, rosters ?? [], holidays ?? []));

    private static EmployeeCalendarProfile Profile(
        long employeeId,
        long workLocationId = Dhaka,
        Shift? defaultShift = null,
        WeeklyOffPattern? pattern = null) =>
        new(employeeId, workLocationId, defaultShift, pattern);

    private static List<DateOnly> WeeklyOffsIn(CalendarService calendar, long employeeId, int year, int month) =>
        [.. Enumerable.Range(1, DateTime.DaysInMonth(year, month))
            .Select(day => new DateOnly(year, month, day))
            .Where(date => calendar.GetDayType(employeeId, date) == DayType.WeeklyOff)];

    private static DateOnly[] Dates(int year, int month, params int[] days) =>
        [.. days.Select(day => new DateOnly(year, month, day))];

    private static Shift NewShift(string code, TimeOnly start, TimeOnly end) => new()
    {
        Code = code,
        Name = code,
        StartTime = start,
        EndTime = end,
        BreakMinutes = 60,
        FullDayMinutes = 420,
        HalfDayThresholdMinutes = 240,
    };

    private static Holiday NewHoliday(string name, DateOnly date, params long[] workLocationIds)
    {
        var holiday = new Holiday { Name = name, HolidayDate = date };
        holiday.SetLocations(workLocationIds);
        return holiday;
    }
}
