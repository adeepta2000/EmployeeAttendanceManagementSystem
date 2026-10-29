namespace Attendance.Domain.Scheduling;

/// <summary>
/// Answers, for one employee and date, what kind of day it is and which shift applies (spec §2.7: the
/// calendar context and effective shift of the processing engine). Pure: it works on a
/// <see cref="CalendarData"/> loaded up front, so processing a month asks the database a handful of
/// questions, not one per employee-day.
/// </summary>
/// <remarks>
/// A date is always the date a shift starts on: a night shift from 22:00 Monday to 06:00 Tuesday is
/// Monday's shift, and Monday's day type applies to all of it (BR-12).
/// </remarks>
public interface ICalendarService
{
    /// <summary>
    /// <see cref="DayType.Holiday"/> if a non-optional holiday applies at the employee's work location;
    /// otherwise <see cref="DayType.WeeklyOff"/> if the roster entry's weekly-off override says so, or,
    /// without an override, the employee's weekly-off pattern; otherwise <see cref="DayType.Working"/>.
    /// A holiday that falls on a weekly off is a holiday (its OT multiplier is the higher one).
    /// </summary>
    DayType GetDayType(long employeeId, DateOnly date);

    /// <summary>
    /// The roster entry's shift if the date is rostered, otherwise the employee's default shift; null if
    /// they have neither. It is returned on weekly offs and holidays too: it frames the punch window when
    /// someone works that day.
    /// </summary>
    Shift? GetEffectiveShift(long employeeId, DateOnly date);

    /// <summary>The day type and effective shift together, with the roster entry and holiday behind them.</summary>
    CalendarDay GetDay(long employeeId, DateOnly date);
}

public sealed class CalendarService : ICalendarService
{
    private readonly DateOnly _from;
    private readonly DateOnly _to;
    private readonly Dictionary<long, EmployeeCalendarProfile> _employees;
    private readonly Dictionary<(long EmployeeId, DateOnly Date), EmployeeShiftRoster> _rosters;
    private readonly ILookup<DateOnly, Holiday> _holidays;

    /// <exception cref="ArgumentException">The range is empty, or an employee or a live roster date appears twice.</exception>
    public CalendarService(CalendarData data)
    {
        if (data.To < data.From)
        {
            throw new ArgumentException("The calendar's range ends before it starts.", nameof(data));
        }

        _from = data.From;
        _to = data.To;
        _employees = data.Employees.ToDictionary(e => e.EmployeeId);
        _rosters = data.Rosters.Where(r => !r.IsDeleted).ToDictionary(r => (r.EmployeeId, r.RosterDate));
        _holidays = data.Holidays.Where(h => !h.IsDeleted).ToLookup(h => h.HolidayDate);
    }

    public DayType GetDayType(long employeeId, DateOnly date) => GetDay(employeeId, date).DayType;

    public Shift? GetEffectiveShift(long employeeId, DateOnly date) => GetDay(employeeId, date).Shift;

    /// <exception cref="ArgumentOutOfRangeException">The date is outside the loaded range.</exception>
    /// <exception cref="ArgumentException">The employee wasn't loaded.</exception>
    public CalendarDay GetDay(long employeeId, DateOnly date)
    {
        // Outside the loaded range a missing roster entry or holiday would look like "none", which is wrong.
        if (date < _from || date > _to)
        {
            throw new ArgumentOutOfRangeException(nameof(date), date, $"The calendar covers {_from:yyyy-MM-dd} to {_to:yyyy-MM-dd} only.");
        }

        if (!_employees.TryGetValue(employeeId, out var employee))
        {
            throw new ArgumentException($"Employee {employeeId} isn't in this calendar.", nameof(employeeId));
        }

        var roster = _rosters.GetValueOrDefault((employeeId, date));
        var holiday = _holidays[date].FirstOrDefault(h => !h.IsOptional && h.AppliesTo(employee.WorkLocationId));
        var isWeeklyOff = roster?.IsWeeklyOffOverride ?? employee.WeeklyOffPattern?.IsWeeklyOff(date) ?? false;

        var dayType = holiday is not null ? DayType.Holiday
            : isWeeklyOff ? DayType.WeeklyOff
            : DayType.Working;

        return new CalendarDay(date, dayType, roster?.Shift ?? employee.DefaultShift, roster, holiday);
    }
}

/// <summary>Everything <see cref="CalendarService"/> needs for a set of employees over a date range.</summary>
/// <param name="From">First date covered, inclusive.</param>
/// <param name="To">Last date covered, inclusive.</param>
/// <param name="Rosters">The roster entries of those employees in the range, each with its <see cref="EmployeeShiftRoster.Shift"/> loaded.</param>
/// <param name="Holidays">The holidays in the range, each with its location links loaded.</param>
public sealed record CalendarData(
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<EmployeeCalendarProfile> Employees,
    IReadOnlyCollection<EmployeeShiftRoster> Rosters,
    IReadOnlyCollection<Holiday> Holidays);

/// <summary>The scheduling facts of one employee: where they work and their default shift and weekly-off pattern.</summary>
/// <param name="WeeklyOffPattern">With its days loaded; null if the employee has no regular weekly off.</param>
public sealed record EmployeeCalendarProfile(
    long EmployeeId,
    long WorkLocationId,
    Shift? DefaultShift,
    WeeklyOffPattern? WeeklyOffPattern);

/// <summary>One employee's date, resolved.</summary>
/// <param name="Shift">The effective shift: see <see cref="ICalendarService.GetEffectiveShift"/>.</param>
/// <param name="Roster">The roster entry for the date, if there is one.</param>
/// <param name="Holiday">The holiday that makes it a <see cref="DayType.Holiday"/>, if any.</param>
public sealed record CalendarDay(
    DateOnly Date,
    DayType DayType,
    Shift? Shift,
    EmployeeShiftRoster? Roster,
    Holiday? Holiday);
