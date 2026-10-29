using Attendance.Application.Organization;
using Attendance.Domain.Scheduling;

namespace Attendance.Application.Scheduling;

// Response DTOs. Records are identified by PublicId only; internal IDs never leave the server.

public sealed record ShiftRef(Guid PublicId, string Code, string Name);

public sealed record WeeklyOffPatternRef(Guid PublicId, string Name);

/// <param name="CrossesMidnight">Derived from the times: true when <paramref name="EndTime"/> is earlier in the day than <paramref name="StartTime"/>.</param>
public sealed record ShiftDto(
    Guid PublicId,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool CrossesMidnight,
    int DurationMinutes,
    int BreakMinutes,
    int GraceInMinutes,
    int GraceOutMinutes,
    int HalfDayThresholdMinutes,
    int FullDayMinutes,
    int PunchWindowBeforeMinutes,
    int PunchWindowAfterMinutes,
    bool IsActive);

/// <param name="WeekOfMonth">1–5 for that occurrence of the weekday in the month only; null for every week.</param>
public sealed record WeeklyOffDayDto(DayOfWeek DayOfWeek, int? WeekOfMonth);

public sealed record WeeklyOffPatternDto(Guid PublicId, string Name, IReadOnlyList<WeeklyOffDayDto> Days, bool IsActive);

/// <param name="WorkLocations">The locations the holiday is limited to; empty when it applies everywhere.</param>
public sealed record HolidayDto(
    Guid PublicId,
    string Name,
    DateOnly HolidayDate,
    int Year,
    bool IsOptional,
    string? Description,
    IReadOnlyList<OrgUnitRef> WorkLocations);

/// <param name="IsWeeklyOffOverride">Null: the weekly-off pattern decides. True: a weekly off. False: a working day.</param>
public sealed record RosterEntryDto(
    Guid PublicId,
    EmployeeRef Employee,
    DateOnly RosterDate,
    ShiftRef Shift,
    bool? IsWeeklyOffOverride,
    string? Remarks);

/// <param name="EmployeeCount">Active employees of the department, within your scope, that the assignment covered.</param>
/// <param name="Created">Roster entries added.</param>
/// <param name="Updated">Existing entries that were changed or restored. Entries that already matched are left alone.</param>
public sealed record BulkRosterResult(int EmployeeCount, int DayCount, int Created, int Updated);

/// <param name="EmployeeCount">Employees whose entries were cleared.</param>
/// <param name="Removed">Roster entries removed; their dates fall back to the default shift and weekly-off pattern.</param>
public sealed record ClearRosterResult(int EmployeeCount, int DayCount, int Removed);

/// <summary>One row of the team roster grid (screen 16): an employee and how each date resolves for them.</summary>
/// <param name="JoiningDate">Dates before it can't be rostered.</param>
/// <param name="Days">One per date of the requested range, in order.</param>
public sealed record TeamCalendarRowDto(
    EmployeeRef Employee,
    OrgUnitRef Department,
    DateOnly JoiningDate,
    ShiftRef? DefaultShift,
    WeeklyOffPatternRef? WeeklyOffPattern,
    IReadOnlyList<CalendarDayDto> Days);

/// <param name="DefaultShift">Null until a default shift is assigned.</param>
/// <param name="WeeklyOffPattern">Null when the employee has no regular weekly off.</param>
public sealed record EmployeeScheduleDto(EmployeeRef Employee, ShiftRef? DefaultShift, WeeklyOffPatternRef? WeeklyOffPattern);

/// <param name="Shift">The effective shift: the rostered one, else the default; null if neither.</param>
/// <param name="IsRostered">True when a roster entry sets the shift or weekly off for the date.</param>
/// <param name="HolidayName">The holiday that makes it a <see cref="DayType.Holiday"/>, if any.</param>
public sealed record CalendarDayDto(DateOnly Date, DayType DayType, ShiftRef? Shift, bool IsRostered, string? HolidayName);
