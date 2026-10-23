using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Common.Security;
using Attendance.Application.Identity.Abstractions;
using Attendance.Application.Organization.Abstractions;
using Attendance.Shared.Results;
using Microsoft.Extensions.Logging;

namespace Attendance.Application.Organization.Employees;

// Every handler resolves the target employee through the caller's scope (FR-8.3). One outside it gets
// the same NotFound as one that doesn't exist, so a write can't be used to probe for employees either.

internal sealed class CreateEmployeeCommandHandler(
    IEmployeeRepository employees,
    EmployeeAssignmentResolver assignments,
    EmployeeScopeResolver scopes,
    IUnitOfWork unitOfWork,
    ILogger<CreateEmployeeCommandHandler> logger)
    : ICommandHandler<CreateEmployeeCommand, Result<EmployeeDto>>
{
    public async Task<Result<EmployeeDto>> HandleAsync(CreateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var code = OrgCodes.Normalize(command.EmployeeCode);
        if (await employees.CodeExistsAsync(code, cancellationToken))
        {
            return EmployeeErrors.CodeTaken(code);
        }

        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var assignment = await assignments.ResolveAsync(command, employee: null, scope, cancellationToken);
        if (!assignment.IsSuccess)
        {
            return assignment.Error;
        }

        var employee = EmployeeFieldRules.CreateEmployee(command);
        employee.Assign(assignment.Value);
        employees.Add(employee);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Employee {EmployeeId} created", employee.PublicId);
        return employee.ToDto(hasUserAccount: false);
    }
}

internal sealed class UpdateEmployeeCommandHandler(
    IEmployeeRepository employees,
    EmployeeAssignmentResolver assignments,
    EmployeeScopeResolver scopes,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateEmployeeCommand, Result<EmployeeDto>>
{
    public async Task<Result<EmployeeDto>> HandleAsync(UpdateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var employee = await employees.FindAsync(command.PublicId, scope, cancellationToken);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        var code = OrgCodes.Normalize(command.EmployeeCode);
        if (code != employee.EmployeeCode && await employees.CodeExistsAsync(code, cancellationToken))
        {
            return EmployeeErrors.CodeTaken(code);
        }

        if (employee.SeparationDate < command.JoiningDate)
        {
            return EmployeeErrors.SeparationBeforeJoining;
        }

        var assignment = await assignments.ResolveAsync(command, employee, scope, cancellationToken);
        if (!assignment.IsSuccess)
        {
            return assignment.Error;
        }

        employee.Apply(command);
        employee.Assign(assignment.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return employee.ToDto(await employees.HasUserAccountAsync(employee.Id, cancellationToken));
    }
}

internal sealed class DeactivateEmployeeCommandHandler(
    IEmployeeRepository employees,
    IUserRepository users,
    EmployeeScopeResolver scopes,
    IUnitOfWork unitOfWork,
    ILogger<DeactivateEmployeeCommandHandler> logger)
    : ICommandHandler<DeactivateEmployeeCommand, Result<EmployeeDto>>
{
    public async Task<Result<EmployeeDto>> HandleAsync(DeactivateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var employee = await employees.FindAsync(command.PublicId, scope, cancellationToken);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        if (!employee.IsActive)
        {
            return EmployeeErrors.AlreadyInactive;
        }

        if (command.SeparationDate < employee.JoiningDate)
        {
            return EmployeeErrors.SeparationBeforeJoining;
        }

        employee.Deactivate(command.SeparationDate);

        // A former employee must not keep signing in. An inactive account fails sign-in and refresh, and
        // the user context rejects it on the very next request, so outstanding access tokens die too.
        var user = await users.FindByEmployeeIdAsync(employee.Id, cancellationToken);
        var accountDeactivated = user is { IsActive: true };
        if (accountDeactivated)
        {
            user!.IsActive = false;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Employee {EmployeeId} deactivated with separation date {SeparationDate}; login account deactivated: {AccountDeactivated}",
            employee.PublicId, command.SeparationDate, accountDeactivated);
        return employee.ToDto(hasUserAccount: user is not null);
    }
}

internal sealed class ReactivateEmployeeCommandHandler(
    IEmployeeRepository employees,
    EmployeeScopeResolver scopes,
    IUnitOfWork unitOfWork,
    ILogger<ReactivateEmployeeCommandHandler> logger)
    : ICommandHandler<ReactivateEmployeeCommand, Result<EmployeeDto>>
{
    public async Task<Result<EmployeeDto>> HandleAsync(ReactivateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var employee = await employees.FindAsync(command.PublicId, scope, cancellationToken);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        if (employee.IsActive)
        {
            return EmployeeErrors.AlreadyActive;
        }

        employee.Reactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Employee {EmployeeId} reactivated", employee.PublicId);
        return employee.ToDto(await employees.HasUserAccountAsync(employee.Id, cancellationToken));
    }
}
