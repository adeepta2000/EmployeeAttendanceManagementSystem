using Attendance.Application.Organization;
using Attendance.Domain.Organization;
using Attendance.Domain.Scheduling;
using Riok.Mapperly.Abstractions;

namespace Attendance.Application.Scheduling;

/// <summary>Entity-to-DTO mappings, generated at compile time by Mapperly (see <see cref="OrganizationMapper"/>).</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
[UseStaticMapper(typeof(OrganizationMapper))]
internal static partial class SchedulingMapper
{
    public static partial ShiftDto ToDto(this Shift shift);

    public static partial ShiftRef ToRef(this Shift shift);

    public static partial WeeklyOffPatternDto ToDto(this WeeklyOffPattern pattern);

    public static partial WeeklyOffPatternRef ToRef(this WeeklyOffPattern pattern);

    /// <remarks>Needs <see cref="EmployeeShiftRoster.Employee"/> and <see cref="EmployeeShiftRoster.Shift"/> loaded.</remarks>
    public static partial RosterEntryDto ToDto(this EmployeeShiftRoster roster);

    /// <remarks>Needs the location links and their work locations loaded.</remarks>
    public static HolidayDto ToDto(this Holiday holiday) => new(
        holiday.PublicId,
        holiday.Name,
        holiday.HolidayDate,
        holiday.Year,
        holiday.IsOptional,
        holiday.Description,
        [.. holiday.Locations.Where(l => !l.IsDeleted).Select(l => l.WorkLocation.ToRef()).OrderBy(l => l.Name)]);

    /// <remarks>Needs <see cref="Employee.DefaultShift"/> and <see cref="Employee.WeeklyOffPattern"/> loaded.</remarks>
    public static EmployeeScheduleDto ToScheduleDto(this Employee employee) =>
        new(employee.ToRef(), employee.DefaultShift?.ToRef(), employee.WeeklyOffPattern?.ToRef());

    public static CalendarDayDto ToDto(this CalendarDay day) =>
        new(day.Date, day.DayType, day.Shift?.ToRef(), day.Roster is not null, day.Holiday?.Name);

    /// <remarks>Needs <see cref="Employee.Department"/>, <see cref="Employee.DefaultShift"/> and <see cref="Employee.WeeklyOffPattern"/> loaded.</remarks>
    public static TeamCalendarRowDto ToTeamCalendarRow(this Employee employee, IReadOnlyList<CalendarDayDto> days) => new(
        employee.ToRef(),
        employee.Department.ToRef(),
        employee.JoiningDate,
        employee.DefaultShift?.ToRef(),
        employee.WeeklyOffPattern?.ToRef(),
        days);

    // Weekdays in calendar order, each weekday's specific weeks after its "every week" entry.
    private static IReadOnlyList<WeeklyOffDayDto> MapDays(IReadOnlyList<WeeklyOffPatternDay> days) =>
        [.. days.OrderBy(d => d.DayOfWeek).ThenBy(d => d.WeekOfMonth).Select(d => new WeeklyOffDayDto(d.DayOfWeek, d.WeekOfMonth))];

    private static partial OrgUnitRef ToRef(this WorkLocation workLocation);

    private static partial OrgUnitRef ToRef(this Department department);
}
