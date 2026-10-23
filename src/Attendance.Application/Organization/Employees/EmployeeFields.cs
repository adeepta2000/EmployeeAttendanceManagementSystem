using Attendance.Domain.Organization;
using FluentValidation;

namespace Attendance.Application.Organization.Employees;

/// <summary>
/// The employee master fields of FR-1.2 that are entered the same way everywhere: through the API and
/// through the Excel import. One set of rules validates them in both places.
/// </summary>
internal interface IEmployeeFields
{
    string EmployeeCode { get; }

    string FirstName { get; }

    string LastName { get; }

    DateOnly DateOfBirth { get; }

    Gender Gender { get; }

    string? Email { get; }

    string? Phone { get; }

    DateOnly JoiningDate { get; }

    DateOnly? ConfirmationDate { get; }

    EmploymentType EmploymentType { get; }
}

internal static class EmployeeFieldRules
{
    /// <summary>The youngest age at which someone may join.</summary>
    public const int MinimumAgeAtJoining = 14;

    /// <summary>A floor for every date, to catch typos such as 0024 for 2024.</summary>
    public static readonly DateOnly EarliestDate = new(1900, 1, 1);

    public static void AddEmployeeFieldRules<T>(this AbstractValidator<T> validator)
        where T : IEmployeeFields
    {
        validator.RuleFor(e => e.EmployeeCode).MustBeCode(Employee.EmployeeCodeMaxLength);
        validator.RuleFor(e => e.FirstName).MustBeName(Employee.NameMaxLength);
        validator.RuleFor(e => e.LastName).MustBeName(Employee.NameMaxLength);
        validator.RuleFor(e => e.Gender).IsInEnum();
        validator.RuleFor(e => e.EmploymentType).IsInEnum();

        validator.RuleFor(e => e.Email!).MaximumLength(Employee.EmailMaxLength).EmailAddress()
            .When(e => !string.IsNullOrWhiteSpace(e.Email));
        validator.RuleFor(e => e.Phone!).MaximumLength(Employee.PhoneMaxLength)
            .Matches(@"^\+?[0-9][0-9 -]{5,19}$").WithMessage("'{PropertyName}' must be digits, spaces or dashes, optionally starting with +.")
            .When(e => !string.IsNullOrWhiteSpace(e.Phone));

        validator.RuleFor(e => e.JoiningDate).NotEmpty().GreaterThanOrEqualTo(EarliestDate);
        validator.RuleFor(e => e.DateOfBirth).NotEmpty().GreaterThanOrEqualTo(EarliestDate);
        validator.RuleFor(e => e.DateOfBirth)
            .Must((e, dateOfBirth) => dateOfBirth.AddYears(MinimumAgeAtJoining) <= e.JoiningDate)
            .WithMessage($"The employee must be at least {MinimumAgeAtJoining} years old on the joining date.")
            .When(e => e.DateOfBirth != default && e.JoiningDate != default);
        validator.RuleFor(e => e.ConfirmationDate)
            .Must((e, confirmation) => confirmation >= e.JoiningDate)
            .WithMessage("'{PropertyName}' can't be before the joining date.")
            .When(e => e.ConfirmationDate.HasValue);
    }

    /// <summary>Copies the fields onto an employee, normalized as stored.</summary>
    public static void Apply(this Employee employee, IEmployeeFields fields)
    {
        employee.EmployeeCode = OrgCodes.Normalize(fields.EmployeeCode);
        employee.FirstName = fields.FirstName.Trim();
        employee.LastName = fields.LastName.Trim();
        employee.DateOfBirth = fields.DateOfBirth;
        employee.Gender = fields.Gender;
        employee.Email = OrgCodes.NullIfBlank(fields.Email)?.ToLowerInvariant();
        employee.Phone = OrgCodes.NullIfBlank(fields.Phone);
        employee.JoiningDate = fields.JoiningDate;
        employee.ConfirmationDate = fields.ConfirmationDate;
        employee.EmploymentType = fields.EmploymentType;
    }

    /// <summary>A new, active employee with the fields applied. The caller sets the department and other links.</summary>
    public static Employee CreateEmployee(IEmployeeFields fields)
    {
        var employee = new Employee
        {
            EmployeeCode = string.Empty,
            FirstName = string.Empty,
            LastName = string.Empty,
            DateOfBirth = fields.DateOfBirth,
            Gender = fields.Gender,
            JoiningDate = fields.JoiningDate,
            EmploymentType = fields.EmploymentType,
        };
        employee.Apply(fields);
        return employee;
    }
}
