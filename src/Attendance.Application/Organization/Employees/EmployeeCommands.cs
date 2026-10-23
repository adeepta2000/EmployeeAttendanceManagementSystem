using Attendance.Application.Common.Messaging;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Organization.Employees;

// The commands carry personal data, so each ToString is overridden to keep it out of logs (CLAUDE.md).

/// <summary>
/// Adds an employee (FR-1.2). No login account is created: an employee can exist without one (FR-1.3).
/// The department, designation and work location must be active; the reporting manager, if given,
/// must be an active employee inside the caller's scope.
/// </summary>
public sealed record CreateEmployeeCommand(
    string EmployeeCode,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? Email,
    string? Phone,
    DateOnly JoiningDate,
    DateOnly? ConfirmationDate,
    EmploymentType EmploymentType,
    Guid DepartmentId,
    Guid DesignationId,
    Guid WorkLocationId,
    Guid? ReportingManagerId)
    : ICommand<Result<EmployeeDto>>, IEmployeeFields, IEmployeeAssignment
{
    public override string ToString() => nameof(CreateEmployeeCommand);
}

/// <summary>
/// Replaces an employee's details. Links that don't change are kept as they are, even if the department or
/// manager they point at has since been retired. Active state changes go through
/// <see cref="DeactivateEmployeeCommand"/> and <see cref="ReactivateEmployeeCommand"/>.
/// </summary>
public sealed record UpdateEmployeeCommand(
    Guid PublicId,
    string EmployeeCode,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? Email,
    string? Phone,
    DateOnly JoiningDate,
    DateOnly? ConfirmationDate,
    EmploymentType EmploymentType,
    Guid DepartmentId,
    Guid DesignationId,
    Guid WorkLocationId,
    Guid? ReportingManagerId)
    : ICommand<Result<EmployeeDto>>, IEmployeeFields, IEmployeeAssignment
{
    public override string ToString() => $"{nameof(UpdateEmployeeCommand)} {{ PublicId = {PublicId} }}";
}

/// <summary>
/// Records an employee's separation (FR-1.5). Nothing is deleted. Their login account, if any, is
/// deactivated too, so a former employee can no longer sign in.
/// </summary>
public sealed record DeactivateEmployeeCommand(Guid PublicId, DateOnly SeparationDate) : ICommand<Result<EmployeeDto>>;

/// <summary>
/// Makes a deactivated employee active again and clears the separation date. Their login account stays
/// as it is: re-enabling sign-in is an account-management decision.
/// </summary>
public sealed record ReactivateEmployeeCommand(Guid PublicId) : ICommand<Result<EmployeeDto>>;

/// <summary>Where an employee sits in the organization, by PublicId.</summary>
internal interface IEmployeeAssignment
{
    Guid DepartmentId { get; }

    Guid DesignationId { get; }

    Guid WorkLocationId { get; }

    Guid? ReportingManagerId { get; }
}

internal static class EmployeeAssignmentRules
{
    public static void AddAssignmentRules<T>(this AbstractValidator<T> validator)
        where T : IEmployeeAssignment
    {
        validator.RuleFor(c => c.DepartmentId).NotEmpty();
        validator.RuleFor(c => c.DesignationId).NotEmpty();
        validator.RuleFor(c => c.WorkLocationId).NotEmpty();
        validator.RuleFor(c => c.ReportingManagerId).NotEqual(Guid.Empty);
    }
}

internal sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        this.AddEmployeeFieldRules();
        this.AddAssignmentRules();
    }
}

internal sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
        this.AddEmployeeFieldRules();
        this.AddAssignmentRules();
    }
}

internal sealed class DeactivateEmployeeCommandValidator : AbstractValidator<DeactivateEmployeeCommand>
{
    public DeactivateEmployeeCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
        RuleFor(c => c.SeparationDate).NotEmpty().GreaterThanOrEqualTo(EmployeeFieldRules.EarliestDate);
    }
}

internal sealed class ReactivateEmployeeCommandValidator : AbstractValidator<ReactivateEmployeeCommand>
{
    public ReactivateEmployeeCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
    }
}
