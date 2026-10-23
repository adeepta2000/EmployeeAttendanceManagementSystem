using Attendance.Application.Common.Messaging;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Organization.Departments;

/// <summary>Adds a department (FR-1.1). Parent and head are optional.</summary>
/// <param name="ParentDepartmentId">PublicId of an active department to nest under.</param>
/// <param name="HeadEmployeeId">PublicId of an active employee inside the caller's scope.</param>
public sealed record CreateDepartmentCommand(string Code, string Name, Guid? ParentDepartmentId, Guid? HeadEmployeeId)
    : ICommand<Result<DepartmentDto>>;

/// <summary>Replaces a department's details. Active state changes go through <see cref="SetDepartmentActiveCommand"/>.</summary>
public sealed record UpdateDepartmentCommand(Guid PublicId, string Code, string Name, Guid? ParentDepartmentId, Guid? HeadEmployeeId)
    : ICommand<Result<DepartmentDto>>;

/// <summary>
/// Retires or restores a department. It is never deleted, because employees' history points at it.
/// Retiring is refused while active employees or active sub-departments remain.
/// </summary>
public sealed record SetDepartmentActiveCommand(Guid PublicId, bool IsActive) : ICommand<Result<DepartmentDto>>;

internal sealed class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(c => c.Code).MustBeCode(Department.CodeMaxLength);
        RuleFor(c => c.Name).MustBeName(Department.NameMaxLength);
        RuleFor(c => c.ParentDepartmentId).NotEqual(Guid.Empty);
        RuleFor(c => c.HeadEmployeeId).NotEqual(Guid.Empty);
    }
}

internal sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
        RuleFor(c => c.Code).MustBeCode(Department.CodeMaxLength);
        RuleFor(c => c.Name).MustBeName(Department.NameMaxLength);
        RuleFor(c => c.ParentDepartmentId).NotEqual(Guid.Empty);
        RuleFor(c => c.HeadEmployeeId).NotEqual(Guid.Empty);
    }
}

internal sealed class SetDepartmentActiveCommandValidator : AbstractValidator<SetDepartmentActiveCommand>
{
    public SetDepartmentActiveCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
    }
}
