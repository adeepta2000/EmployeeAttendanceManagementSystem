using Attendance.Application.Scheduling.EmployeeSchedules;
using Attendance.Application.Scheduling.Holidays;
using Attendance.Application.Scheduling.Rosters;
using Attendance.Application.Scheduling.Shifts;
using Attendance.Application.Scheduling.WeeklyOffPatterns;
using Attendance.Domain.Scheduling;

namespace Attendance.Api.Contracts.Scheduling;

// Request bodies are separate from the commands: the PublicId of an update comes from the route, and
// binding the command directly would let the body set it. References to other records are PublicIds.

/// <param name="StartTime">Asia/Dhaka wall-clock time in whole minutes, e.g. "22:00" (returned as "22:00:00").</param>
/// <param name="EndTime">Earlier in the day than the start for a shift that runs past midnight.</param>
/// <param name="CrossesMidnight">Optional. Derived from the times; if sent, it must agree with them.</param>
public sealed record ShiftRequest(
    string Code,
    string Name,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool? CrossesMidnight,
    int BreakMinutes,
    int GraceInMinutes,
    int GraceOutMinutes,
    int HalfDayThresholdMinutes,
    int FullDayMinutes,
    int PunchWindowBeforeMinutes,
    int PunchWindowAfterMinutes)
{
    public CreateShiftCommand ToCreateCommand() => new(
        Code, Name, StartTime, EndTime, CrossesMidnight, BreakMinutes, GraceInMinutes, GraceOutMinutes,
        HalfDayThresholdMinutes, FullDayMinutes, PunchWindowBeforeMinutes, PunchWindowAfterMinutes);

    public UpdateShiftCommand ToUpdateCommand(Guid publicId) => new(
        publicId, Code, Name, StartTime, EndTime, CrossesMidnight, BreakMinutes, GraceInMinutes, GraceOutMinutes,
        HalfDayThresholdMinutes, FullDayMinutes, PunchWindowBeforeMinutes, PunchWindowAfterMinutes);
}

/// <param name="WeekOfMonth">1–5 for that occurrence of the weekday in the month only (2 and 4 for alternate Saturdays); omit for every week.</param>
public sealed record WeeklyOffDayRequest(DayOfWeek DayOfWeek, int? WeekOfMonth);

/// <param name="Days">Fixed once the pattern exists.</param>
public sealed record CreateWeeklyOffPatternRequest(string Name, IReadOnlyList<WeeklyOffDayRequest>? Days)
{
    public CreateWeeklyOffPatternCommand ToCommand() =>
        new(Name, [.. (Days ?? []).Select(d => new WeeklyOffDay(d.DayOfWeek, d.WeekOfMonth))]);
}

public sealed record RenameWeeklyOffPatternRequest(string Name);

/// <param name="WorkLocationIds">PublicIds of the work locations the holiday is limited to; omit or leave empty for everywhere.</param>
public sealed record HolidayRequest(
    string Name,
    DateOnly HolidayDate,
    bool IsOptional,
    string? Description,
    IReadOnlyList<Guid>? WorkLocationIds)
{
    public CreateHolidayCommand ToCreateCommand() => new(Name, HolidayDate, IsOptional, Description, WorkLocationIds);

    public UpdateHolidayCommand ToUpdateCommand(Guid publicId) =>
        new(publicId, Name, HolidayDate, IsOptional, Description, WorkLocationIds);
}

/// <param name="EmployeeId">PublicId of an active employee within your scope.</param>
/// <param name="ShiftId">PublicId of an active shift.</param>
/// <param name="IsWeeklyOffOverride">Omit to let the weekly-off pattern decide; true for a weekly off; false for a working day.</param>
public sealed record CreateRosterEntryRequest(
    Guid EmployeeId,
    DateOnly RosterDate,
    Guid ShiftId,
    bool? IsWeeklyOffOverride,
    string? Remarks)
{
    public CreateRosterEntryCommand ToCommand() => new(EmployeeId, RosterDate, ShiftId, IsWeeklyOffOverride, Remarks);
}

/// <param name="IsWeeklyOffOverride">Omit to let the weekly-off pattern decide; true for a weekly off; false for a working day.</param>
public sealed record UpdateRosterEntryRequest(Guid ShiftId, bool? IsWeeklyOffOverride, string? Remarks)
{
    public UpdateRosterEntryCommand ToCommand(Guid publicId) => new(publicId, ShiftId, IsWeeklyOffOverride, Remarks);
}

/// <param name="DepartmentId">PublicId of the department whose active employees are rostered.</param>
/// <param name="ToDate">At most 92 days after <paramref name="FromDate"/>, both included.</param>
public sealed record BulkRosterRequest(
    Guid DepartmentId,
    DateOnly FromDate,
    DateOnly ToDate,
    Guid ShiftId,
    bool? IsWeeklyOffOverride,
    string? Remarks)
{
    public BulkAssignRosterCommand ToCommand() =>
        new(DepartmentId, FromDate, ToDate, ShiftId, IsWeeklyOffOverride, Remarks);
}

/// <param name="EmployeeIds">PublicIds of active employees within your scope, at most 100.</param>
/// <param name="ToDate">At most 92 days after <paramref name="FromDate"/>, both included.</param>
/// <param name="IsWeeklyOffOverride">Omit to let the weekly-off pattern decide; true for a weekly off; false for a working day.</param>
public sealed record AssignRosterRequest(
    IReadOnlyList<Guid>? EmployeeIds,
    DateOnly FromDate,
    DateOnly ToDate,
    Guid ShiftId,
    bool? IsWeeklyOffOverride,
    string? Remarks)
{
    public AssignRosterCommand ToCommand() =>
        new(EmployeeIds ?? [], FromDate, ToDate, ShiftId, IsWeeklyOffOverride, Remarks);
}

/// <param name="EmployeeIds">PublicIds of active employees within your scope, at most 100.</param>
/// <param name="ToDate">At most 92 days after <paramref name="FromDate"/>, both included.</param>
public sealed record ClearRosterRequest(IReadOnlyList<Guid>? EmployeeIds, DateOnly FromDate, DateOnly ToDate)
{
    public ClearRosterCommand ToCommand() => new(EmployeeIds ?? [], FromDate, ToDate);
}

/// <param name="DefaultShiftId">PublicId of an active shift; omit or null to clear.</param>
/// <param name="WeeklyOffPatternId">PublicId of an active weekly-off pattern; omit or null to clear.</param>
public sealed record EmployeeScheduleRequest(Guid? DefaultShiftId, Guid? WeeklyOffPatternId)
{
    public SetEmployeeScheduleCommand ToCommand(Guid employeeId) => new(employeeId, DefaultShiftId, WeeklyOffPatternId);
}

/// <summary>Query-string parameters of <c>GET /api/rosters</c>.</summary>
public sealed class RosterListRequest
{
    public DateOnly FromDate { get; init; }

    /// <summary>At most 92 days after <see cref="FromDate"/>, both included.</summary>
    public DateOnly ToDate { get; init; }

    /// <summary>PublicId of an employee.</summary>
    public Guid? EmployeeId { get; init; }

    /// <summary>PublicId of a department: the entries of its current employees.</summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>1-based.</summary>
    public int Page { get; init; } = 1;

    /// <summary>At most 500.</summary>
    public int PageSize { get; init; } = ListRosterEntriesQuery.DefaultPageSize;

    public ListRosterEntriesQuery ToQuery() => new(FromDate, ToDate, EmployeeId, DepartmentId, Page, PageSize);
}

/// <summary>Query-string parameters of <c>GET /api/rosters/calendar</c>.</summary>
public sealed class TeamCalendarRequest
{
    public DateOnly FromDate { get; init; }

    /// <summary>At most 92 days after <see cref="FromDate"/>, both included.</summary>
    public DateOnly ToDate { get; init; }

    /// <summary>PublicId of a department: only its own employees.</summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>1-based.</summary>
    public int Page { get; init; } = 1;

    /// <summary>At most 100.</summary>
    public int PageSize { get; init; } = ListTeamCalendarQuery.DefaultPageSize;

    public ListTeamCalendarQuery ToQuery() => new(FromDate, ToDate, DepartmentId, Page, PageSize);
}
