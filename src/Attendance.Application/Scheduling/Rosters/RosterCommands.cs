using Attendance.Application.Common.Messaging;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Scheduling.Rosters;

/// <summary>
/// Rosters a shift for one employee on one date, overriding their default shift (FR-2.3). An employee has
/// at most one entry per date: a second one is refused (409), so change the first instead.
/// </summary>
/// <param name="EmployeeId">PublicId of an active employee within the caller's scope (FR-8.3).</param>
/// <param name="ShiftId">PublicId of an active shift.</param>
/// <param name="IsWeeklyOffOverride">Null: the weekly-off pattern decides. True: a weekly off. False: a working day even if the pattern says otherwise.</param>
public sealed record CreateRosterEntryCommand(
    Guid EmployeeId,
    DateOnly RosterDate,
    Guid ShiftId,
    bool? IsWeeklyOffOverride,
    string? Remarks)
    : ICommand<Result<RosterEntryDto>>;

/// <summary>
/// Changes a roster entry's shift, weekly-off override or remarks. The employee and date are the entry's
/// identity and can't change. A shift it already uses stays acceptable after that shift retires.
/// </summary>
public sealed record UpdateRosterEntryCommand(Guid PublicId, Guid ShiftId, bool? IsWeeklyOffOverride, string? Remarks)
    : ICommand<Result<RosterEntryDto>>;

/// <summary>Removes a roster entry (a soft delete); the date falls back to the default shift and weekly-off pattern.</summary>
public sealed record DeleteRosterEntryCommand(Guid PublicId) : ICommand<Result>;

/// <summary>
/// Rosters one shift for every active employee of a department, within the caller's scope, over a date
/// range of up to <see cref="ScheduleLimits.MaxRangeDays"/> days (FR-2.4). Existing entries in the range
/// are overwritten; dates before an employee joined are skipped.
/// </summary>
/// <param name="DepartmentId">PublicId of the department. Only its own employees are rostered, not those of sub-departments.</param>
public sealed record BulkAssignRosterCommand(
    Guid DepartmentId,
    DateOnly FromDate,
    DateOnly ToDate,
    Guid ShiftId,
    bool? IsWeeklyOffOverride,
    string? Remarks)
    : ICommand<Result<BulkRosterResult>>;

/// <summary>
/// Rosters one shift for a selection of employees over a date range: a block drag-selected on the team
/// roster (screen 16). The employees must be active and in the caller's scope; otherwise it is the same
/// set-based assignment as <see cref="BulkAssignRosterCommand"/>, so entries in the range are overwritten
/// and dates before an employee joined are skipped.
/// </summary>
/// <param name="EmployeeIds">PublicIds, at most <see cref="RosterSelection.MaxEmployees"/>.</param>
public sealed record AssignRosterCommand(
    IReadOnlyList<Guid> EmployeeIds,
    DateOnly FromDate,
    DateOnly ToDate,
    Guid ShiftId,
    bool? IsWeeklyOffOverride,
    string? Remarks)
    : ICommand<Result<BulkRosterResult>>;

/// <summary>
/// Removes the roster entries of a selection of employees over a date range, so those dates fall back to
/// each employee's default shift and weekly-off pattern. Requests a recalculation of what it removed.
/// </summary>
/// <param name="EmployeeIds">PublicIds of active employees in the caller's scope, at most <see cref="RosterSelection.MaxEmployees"/>.</param>
public sealed record ClearRosterCommand(IReadOnlyList<Guid> EmployeeIds, DateOnly FromDate, DateOnly ToDate)
    : ICommand<Result<ClearRosterResult>>;

public static class RosterSelection
{
    /// <summary>A page of the team roster grid.</summary>
    public const int MaxEmployees = ListTeamCalendarQuery.MaxPageSize;
}

internal static class RosterSelectionRules
{
    public static IRuleBuilderOptions<T, IReadOnlyList<Guid>> MustBeEmployeeSelection<T>(this IRuleBuilder<T, IReadOnlyList<Guid>> rule) =>
        rule.NotEmpty()
            .Must(ids => ids.Count <= RosterSelection.MaxEmployees)
            .WithMessage($"'{{PropertyName}}' can list at most {RosterSelection.MaxEmployees} employees.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("'{PropertyName}' lists an employee more than once.")
            .Must(ids => !ids.Contains(Guid.Empty))
            .WithMessage("'{PropertyName}' must not contain an empty ID.");
}

internal sealed class AssignRosterCommandValidator : AbstractValidator<AssignRosterCommand>
{
    public AssignRosterCommandValidator()
    {
        RuleFor(c => c.EmployeeIds).MustBeEmployeeSelection();
        RuleFor(c => c.ShiftId).NotEmpty();
        RuleFor(c => c.FromDate).MustBeScheduleDate();
        RuleFor(c => c.ToDate).MustBeScheduleDate().MustEndWithinRange(c => c.FromDate);
        RuleFor(c => c.Remarks).MaximumLength(EmployeeShiftRoster.RemarksMaxLength);
    }
}

internal sealed class ClearRosterCommandValidator : AbstractValidator<ClearRosterCommand>
{
    public ClearRosterCommandValidator()
    {
        RuleFor(c => c.EmployeeIds).MustBeEmployeeSelection();
        RuleFor(c => c.FromDate).MustBeScheduleDate();
        RuleFor(c => c.ToDate).MustBeScheduleDate().MustEndWithinRange(c => c.FromDate);
    }
}

internal sealed class CreateRosterEntryCommandValidator : AbstractValidator<CreateRosterEntryCommand>
{
    public CreateRosterEntryCommandValidator()
    {
        RuleFor(c => c.EmployeeId).NotEmpty();
        RuleFor(c => c.RosterDate).MustBeScheduleDate();
        RuleFor(c => c.ShiftId).NotEmpty();
        RuleFor(c => c.Remarks).MaximumLength(EmployeeShiftRoster.RemarksMaxLength);
    }
}

internal sealed class UpdateRosterEntryCommandValidator : AbstractValidator<UpdateRosterEntryCommand>
{
    public UpdateRosterEntryCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
        RuleFor(c => c.ShiftId).NotEmpty();
        RuleFor(c => c.Remarks).MaximumLength(EmployeeShiftRoster.RemarksMaxLength);
    }
}

internal sealed class DeleteRosterEntryCommandValidator : AbstractValidator<DeleteRosterEntryCommand>
{
    public DeleteRosterEntryCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
    }
}

internal sealed class BulkAssignRosterCommandValidator : AbstractValidator<BulkAssignRosterCommand>
{
    public BulkAssignRosterCommandValidator()
    {
        RuleFor(c => c.DepartmentId).NotEmpty();
        RuleFor(c => c.ShiftId).NotEmpty();
        RuleFor(c => c.FromDate).MustBeScheduleDate();
        RuleFor(c => c.ToDate).MustBeScheduleDate().MustEndWithinRange(c => c.FromDate);
        RuleFor(c => c.Remarks).MaximumLength(EmployeeShiftRoster.RemarksMaxLength);
    }
}
