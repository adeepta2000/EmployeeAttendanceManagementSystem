using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Common.Security;
using Attendance.Application.Organization;
using Attendance.Application.Organization.Abstractions;
using Attendance.Application.Scheduling.Abstractions;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Scheduling.EmployeeSchedules;

/// <summary>An employee's default shift and weekly-off pattern, if the caller's scope includes them (FR-8.3).</summary>
public sealed record GetEmployeeScheduleQuery(Guid EmployeeId) : IQuery<Result<EmployeeScheduleDto>>;

/// <summary>
/// Assigns an employee's default shift and weekly-off pattern (FR-2.2, FR-2.3); null clears either. An
/// unchanged assignment stays acceptable after its shift or pattern retires; a new one must be active.
/// </summary>
/// <remarks>
/// Neither is effective-dated (spec §3.2), so the change applies to every date processed from now on.
/// Unlike a roster change it requests no recalculation: reprocessing history under the new shift would
/// rewrite it. To change a date that is already processed, roster it.
/// </remarks>
public sealed record SetEmployeeScheduleCommand(Guid EmployeeId, Guid? DefaultShiftId, Guid? WeeklyOffPatternId)
    : ICommand<Result<EmployeeScheduleDto>>;

/// <summary>
/// How each date from <see cref="FromDate"/> to <see cref="ToDate"/> resolves for an employee in the
/// caller's scope: the day type and the effective shift (<see cref="ICalendarService"/>).
/// </summary>
public sealed record GetEmployeeCalendarQuery(Guid EmployeeId, DateOnly FromDate, DateOnly ToDate)
    : IQuery<Result<IReadOnlyList<CalendarDayDto>>>;

internal sealed class GetEmployeeScheduleQueryValidator : AbstractValidator<GetEmployeeScheduleQuery>
{
    public GetEmployeeScheduleQueryValidator()
    {
        RuleFor(q => q.EmployeeId).NotEmpty();
    }
}

internal sealed class SetEmployeeScheduleCommandValidator : AbstractValidator<SetEmployeeScheduleCommand>
{
    public SetEmployeeScheduleCommandValidator()
    {
        RuleFor(c => c.EmployeeId).NotEmpty();
        RuleFor(c => c.DefaultShiftId).NotEqual(Guid.Empty);
        RuleFor(c => c.WeeklyOffPatternId).NotEqual(Guid.Empty);
    }
}

internal sealed class GetEmployeeCalendarQueryValidator : AbstractValidator<GetEmployeeCalendarQuery>
{
    public GetEmployeeCalendarQueryValidator()
    {
        RuleFor(q => q.EmployeeId).NotEmpty();
        RuleFor(q => q.FromDate).MustBeScheduleDate();
        RuleFor(q => q.ToDate).MustBeScheduleDate().MustEndWithinRange(q => q.FromDate);
    }
}

internal sealed class GetEmployeeScheduleQueryHandler(IEmployeeRepository employees, EmployeeScopeResolver scopes)
    : IQueryHandler<GetEmployeeScheduleQuery, Result<EmployeeScheduleDto>>
{
    public async Task<Result<EmployeeScheduleDto>> HandleAsync(GetEmployeeScheduleQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        return await employees.FindAsync(query.EmployeeId, scope, cancellationToken) is { } employee
            ? employee.ToScheduleDto()
            : EmployeeErrors.NotFound;
    }
}

internal sealed class SetEmployeeScheduleCommandHandler(
    IEmployeeRepository employees,
    IMasterDataRepository<Shift> shifts,
    IWeeklyOffPatternRepository patterns,
    EmployeeScopeResolver scopes,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetEmployeeScheduleCommand, Result<EmployeeScheduleDto>>
{
    public async Task<Result<EmployeeScheduleDto>> HandleAsync(SetEmployeeScheduleCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var employee = await employees.FindAsync(command.EmployeeId, scope, cancellationToken);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        // Only a changed assignment is looked up, so an unchanged one survives its shift or pattern retiring.
        var shift = employee.DefaultShift;
        if (command.DefaultShiftId != shift?.PublicId)
        {
            shift = command.DefaultShiftId is { } shiftId ? await shifts.FindAsync(shiftId, cancellationToken) : null;
            if (command.DefaultShiftId is not null && shift is not { IsActive: true })
            {
                return EmployeeScheduleErrors.ShiftNotFound;
            }
        }

        var pattern = employee.WeeklyOffPattern;
        if (command.WeeklyOffPatternId != pattern?.PublicId)
        {
            pattern = command.WeeklyOffPatternId is { } patternId ? await patterns.FindAsync(patternId, cancellationToken) : null;
            if (command.WeeklyOffPatternId is not null && pattern is not { IsActive: true })
            {
                return EmployeeScheduleErrors.WeeklyOffPatternNotFound;
            }
        }

        employee.DefaultShift = shift;
        employee.DefaultShiftId = shift?.Id;
        employee.WeeklyOffPattern = pattern;
        employee.WeeklyOffPatternId = pattern?.Id;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return employee.ToScheduleDto();
    }
}

internal sealed class GetEmployeeCalendarQueryHandler(
    IEmployeeRepository employees,
    ICalendarRepository calendars,
    EmployeeScopeResolver scopes)
    : IQueryHandler<GetEmployeeCalendarQuery, Result<IReadOnlyList<CalendarDayDto>>>
{
    public async Task<Result<IReadOnlyList<CalendarDayDto>>> HandleAsync(GetEmployeeCalendarQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var employee = await employees.FindAsync(query.EmployeeId, scope, cancellationToken);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        var calendar = new CalendarService(await calendars.LoadAsync([employee.Id], query.FromDate, query.ToDate, cancellationToken));
        var days = ScheduleLimits.DayCount(query.FromDate, query.ToDate);

        return Result<IReadOnlyList<CalendarDayDto>>.Success(
            [.. Enumerable.Range(0, days).Select(offset => calendar.GetDay(employee.Id, query.FromDate.AddDays(offset)).ToDto())]);
    }
}
