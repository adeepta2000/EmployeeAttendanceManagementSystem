using Attendance.Application.Common.Messaging;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Organization.Designations;

/// <summary>Adds a designation (job title) with an optional pay grade (FR-1.1).</summary>
public sealed record CreateDesignationCommand(string Code, string Name, string? Grade) : ICommand<Result<DesignationDto>>;

public sealed record UpdateDesignationCommand(Guid PublicId, string Code, string Name, string? Grade) : ICommand<Result<DesignationDto>>;

/// <summary>Retires or restores a designation. Retiring is refused while active employees hold it.</summary>
public sealed record SetDesignationActiveCommand(Guid PublicId, bool IsActive) : ICommand<Result<DesignationDto>>;

internal sealed class CreateDesignationCommandValidator : AbstractValidator<CreateDesignationCommand>
{
    public CreateDesignationCommandValidator()
    {
        RuleFor(c => c.Code).MustBeCode(Designation.CodeMaxLength);
        RuleFor(c => c.Name).MustBeName(Designation.NameMaxLength);
        RuleFor(c => c.Grade).MaximumLength(Designation.GradeMaxLength);
    }
}

internal sealed class UpdateDesignationCommandValidator : AbstractValidator<UpdateDesignationCommand>
{
    public UpdateDesignationCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
        RuleFor(c => c.Code).MustBeCode(Designation.CodeMaxLength);
        RuleFor(c => c.Name).MustBeName(Designation.NameMaxLength);
        RuleFor(c => c.Grade).MaximumLength(Designation.GradeMaxLength);
    }
}

internal sealed class SetDesignationActiveCommandValidator : AbstractValidator<SetDesignationActiveCommand>
{
    public SetDesignationActiveCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
    }
}
