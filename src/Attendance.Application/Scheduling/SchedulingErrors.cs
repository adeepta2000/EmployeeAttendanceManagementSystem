using Attendance.Shared.Results;

namespace Attendance.Application.Scheduling;

public static class ShiftErrors
{
    public static readonly Error NotFound = new("shift.not_found", "The shift was not found.", ErrorType.NotFound);

    public static readonly Error InUse = new(
        "shift.in_use", "Active employees have this as their default shift. Assign them another before deactivating it.", ErrorType.Conflict);

    public static Error CodeTaken(string code) =>
        new("shift.code_taken", $"Shift code '{code}' is already in use.", ErrorType.Conflict);
}

public static class WeeklyOffPatternErrors
{
    public static readonly Error NotFound = new("weekly_off_pattern.not_found", "The weekly-off pattern was not found.", ErrorType.NotFound);

    public static readonly Error InUse = new(
        "weekly_off_pattern.in_use", "Active employees follow this pattern. Assign them another before deactivating it.", ErrorType.Conflict);

    public static Error NameTaken(string name) =>
        new("weekly_off_pattern.name_taken", $"A weekly-off pattern named '{name}' already exists.", ErrorType.Conflict);
}

public static class HolidayErrors
{
    public static readonly Error NotFound = new("holiday.not_found", "The holiday was not found.", ErrorType.NotFound);

    public static readonly Error WorkLocationNotFound = new(
        "holiday.work_location_not_found", "A work location was not found or is inactive.", ErrorType.Validation);

    public static Error Duplicate(DateOnly date, string name) =>
        new("holiday.duplicate", $"'{name}' is already in the calendar on {date:yyyy-MM-dd}.", ErrorType.Conflict);
}

public static class RosterErrors
{
    /// <summary>Also returned for an entry of an employee outside the caller's scope (FR-8.3).</summary>
    public static readonly Error NotFound = new("roster.not_found", "The roster entry was not found.", ErrorType.NotFound);

    public static readonly Error EmployeeNotFound = new(
        "roster.employee_not_found", "The employee was not found or is inactive.", ErrorType.Validation);

    public static readonly Error ShiftNotFound = new("roster.shift_not_found", "The shift was not found or is inactive.", ErrorType.Validation);

    public static readonly Error DepartmentNotFound = new("roster.department_not_found", "The department was not found.", ErrorType.Validation);

    public static readonly Error BeforeJoining = new(
        "roster.before_joining", "The employee hadn't joined yet on that date.", ErrorType.Validation);

    public static Error DateTaken(DateOnly date) =>
        new("roster.date_taken", $"The employee is already rostered on {date:yyyy-MM-dd}. Change that entry instead.", ErrorType.Conflict);
}

public static class EmployeeScheduleErrors
{
    public static readonly Error ShiftNotFound = new(
        "employee.shift_not_found", "The default shift was not found or is inactive.", ErrorType.Validation);

    public static readonly Error WeeklyOffPatternNotFound = new(
        "employee.weekly_off_pattern_not_found", "The weekly-off pattern was not found or is inactive.", ErrorType.Validation);
}
