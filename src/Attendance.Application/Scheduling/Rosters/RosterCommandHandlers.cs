using Attendance.Application.Common.Events;
using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Common.Security;
using Attendance.Application.Organization;
using Attendance.Application.Organization.Abstractions;
using Attendance.Application.Scheduling.Abstractions;
using Attendance.Domain.Processing;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Results;
using Microsoft.Extensions.Logging;

namespace Attendance.Application.Scheduling.Rosters;

// Every handler resolves the employee through the caller's scope (FR-8.3): a manager rosters their own
// reporting tree, HR the whole company. Each change to an entry raises AttendanceRecalculationRequested
// (FR-2.5); the unit of work dispatches it once the change is saved.

internal sealed class CreateRosterEntryCommandHandler(
    IRosterRepository rosters,
    IEmployeeRepository employees,
    IMasterDataRepository<Shift> shifts,
    EmployeeScopeResolver scopes,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateRosterEntryCommand, Result<RosterEntryDto>>
{
    public async Task<Result<RosterEntryDto>> HandleAsync(CreateRosterEntryCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var employee = await employees.FindAsync(command.EmployeeId, scope, cancellationToken);
        if (employee is not { IsActive: true })
        {
            return RosterErrors.EmployeeNotFound;
        }

        if (command.RosterDate < employee.JoiningDate)
        {
            return RosterErrors.BeforeJoining;
        }

        var shift = await shifts.FindAsync(command.ShiftId, cancellationToken);
        if (shift is not { IsActive: true })
        {
            return RosterErrors.ShiftNotFound;
        }

        var remarks = OrgCodes.NullIfBlank(command.Remarks);
        var roster = await rosters.FindForDateAsync(employee.Id, command.RosterDate, cancellationToken);
        if (roster is { IsDeleted: false })
        {
            return RosterErrors.DateTaken(command.RosterDate);
        }

        // The unique index allows one row per employee and date, so a removed entry comes back rather than
        // a second row being added.
        if (roster is not null)
        {
            roster.Restore(shift, command.IsWeeklyOffOverride, remarks);
        }
        else
        {
            roster = EmployeeShiftRoster.Create(employee.Id, command.RosterDate, shift, command.IsWeeklyOffOverride, remarks);
            rosters.Add(roster);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return roster.ToDto();
    }
}

internal sealed class UpdateRosterEntryCommandHandler(
    IRosterRepository rosters,
    IMasterDataRepository<Shift> shifts,
    EmployeeScopeResolver scopes,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateRosterEntryCommand, Result<RosterEntryDto>>
{
    public async Task<Result<RosterEntryDto>> HandleAsync(UpdateRosterEntryCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var roster = await rosters.FindAsync(command.PublicId, scope, cancellationToken);
        if (roster is null)
        {
            return RosterErrors.NotFound;
        }

        var keepsShift = roster.Shift.PublicId == command.ShiftId;
        var shift = keepsShift ? roster.Shift : await shifts.FindAsync(command.ShiftId, cancellationToken);
        if (shift is null || !(keepsShift || shift.IsActive))
        {
            return RosterErrors.ShiftNotFound;
        }

        roster.Update(shift, command.IsWeeklyOffOverride, OrgCodes.NullIfBlank(command.Remarks));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return roster.ToDto();
    }
}

internal sealed class DeleteRosterEntryCommandHandler(IRosterRepository rosters, EmployeeScopeResolver scopes, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteRosterEntryCommand, Result>
{
    public async Task<Result> HandleAsync(DeleteRosterEntryCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var roster = await rosters.FindAsync(command.PublicId, scope, cancellationToken);
        if (roster is null)
        {
            return Result.Failure(RosterErrors.NotFound);
        }

        roster.Remove();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class BulkAssignRosterCommandHandler(
    IRosterRepository rosters,
    IDepartmentRepository departments,
    IMasterDataRepository<Shift> shifts,
    EmployeeScopeResolver scopes,
    IDomainEventDispatcher domainEvents,
    ILogger<BulkAssignRosterCommandHandler> logger)
    : ICommandHandler<BulkAssignRosterCommand, Result<BulkRosterResult>>
{
    public async Task<Result<BulkRosterResult>> HandleAsync(BulkAssignRosterCommand command, CancellationToken cancellationToken)
    {
        var department = await departments.FindAsync(command.DepartmentId, cancellationToken);
        if (department is null)
        {
            return RosterErrors.DepartmentNotFound;
        }

        var shift = await shifts.FindAsync(command.ShiftId, cancellationToken);
        if (shift is not { IsActive: true })
        {
            return RosterErrors.ShiftNotFound;
        }

        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var assignment = new RosterAssignment(
            command.FromDate, command.ToDate, shift.Id, command.IsWeeklyOffOverride, OrgCodes.NullIfBlank(command.Remarks));
        var outcome = await rosters.AssignToDepartmentAsync(department.Id, assignment, scope, cancellationToken);
        await domainEvents.RequestRecalculationAsync(outcome, cancellationToken);

        logger.LogInformation(
            "Shift {ShiftId} rostered for department {DepartmentId} from {From} to {To}: {EmployeeCount} employees, {Created} entries created, {Updated} updated",
            shift.PublicId, department.PublicId, command.FromDate, command.ToDate, outcome.EmployeeCount, outcome.Created, outcome.Updated);

        return new BulkRosterResult(
            outcome.EmployeeCount, ScheduleLimits.DayCount(command.FromDate, command.ToDate), outcome.Created, outcome.Updated);
    }
}

internal sealed class AssignRosterCommandHandler(
    IRosterRepository rosters,
    IEmployeeRepository employees,
    IMasterDataRepository<Shift> shifts,
    EmployeeScopeResolver scopes,
    IDomainEventDispatcher domainEvents,
    ILogger<AssignRosterCommandHandler> logger)
    : ICommandHandler<AssignRosterCommand, Result<BulkRosterResult>>
{
    public async Task<Result<BulkRosterResult>> HandleAsync(AssignRosterCommand command, CancellationToken cancellationToken)
    {
        // All or nothing: one employee outside the scope, inactive or unknown refuses the whole selection.
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var employeeIds = await employees.FindActiveIdsAsync(command.EmployeeIds, scope, cancellationToken);
        if (employeeIds.Count != command.EmployeeIds.Count)
        {
            return RosterErrors.EmployeeNotFound;
        }

        var shift = await shifts.FindAsync(command.ShiftId, cancellationToken);
        if (shift is not { IsActive: true })
        {
            return RosterErrors.ShiftNotFound;
        }

        var assignment = new RosterAssignment(
            command.FromDate, command.ToDate, shift.Id, command.IsWeeklyOffOverride, OrgCodes.NullIfBlank(command.Remarks));
        var outcome = await rosters.AssignToEmployeesAsync(employeeIds, assignment, cancellationToken);
        await domainEvents.RequestRecalculationAsync(outcome, cancellationToken);

        logger.LogInformation(
            "Shift {ShiftId} rostered for {EmployeeCount} selected employees from {From} to {To}: {Created} entries created, {Updated} updated",
            shift.PublicId, outcome.EmployeeCount, command.FromDate, command.ToDate, outcome.Created, outcome.Updated);

        return new BulkRosterResult(
            outcome.EmployeeCount, ScheduleLimits.DayCount(command.FromDate, command.ToDate), outcome.Created, outcome.Updated);
    }
}

internal sealed class ClearRosterCommandHandler(
    IRosterRepository rosters,
    IEmployeeRepository employees,
    EmployeeScopeResolver scopes,
    IDomainEventDispatcher domainEvents,
    ILogger<ClearRosterCommandHandler> logger)
    : ICommandHandler<ClearRosterCommand, Result<ClearRosterResult>>
{
    public async Task<Result<ClearRosterResult>> HandleAsync(ClearRosterCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var employeeIds = await employees.FindActiveIdsAsync(command.EmployeeIds, scope, cancellationToken);
        if (employeeIds.Count != command.EmployeeIds.Count)
        {
            return RosterErrors.EmployeeNotFound;
        }

        var outcome = await rosters.RemoveAsync(employeeIds, command.FromDate, command.ToDate, cancellationToken);
        await domainEvents.RequestRecalculationAsync(outcome, cancellationToken);

        logger.LogInformation(
            "Roster cleared for {EmployeeCount} selected employees from {From} to {To}: {Removed} entries removed",
            outcome.EmployeeCount, command.FromDate, command.ToDate, outcome.Updated);

        return new ClearRosterResult(outcome.EmployeeCount, ScheduleLimits.DayCount(command.FromDate, command.ToDate), outcome.Updated);
    }
}

internal static class RosterRecalculation
{
    /// <summary>
    /// Set-based writes have no entities to raise the event, so it is raised here: one event covering every
    /// employee and date that changed, and none when nothing did.
    /// </summary>
    public static Task RequestRecalculationAsync(
        this IDomainEventDispatcher domainEvents, RosterAssignmentOutcome outcome, CancellationToken cancellationToken) =>
        outcome is { FirstChangedDate: { } first, LastChangedDate: { } last }
            ? domainEvents.DispatchAsync(
                [new AttendanceRecalculationRequested(outcome.ChangedEmployeeIds, first, last, RecalculationReason.RosterChanged)],
                cancellationToken)
            : Task.CompletedTask;
}
