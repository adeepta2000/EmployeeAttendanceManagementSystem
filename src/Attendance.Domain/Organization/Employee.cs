using Attendance.Domain.Common;
using Attendance.Domain.Scheduling;

namespace Attendance.Domain.Organization;

/// <summary>
/// The employee master (FR-1.2). An employee may exist without a login (FR-1.3), and is retired with
/// <see cref="IsActive"/> = false so their history is preserved (FR-1.5).
/// </summary>
public sealed class Employee : BaseEntity, IHasPublicId
{
    public const int EmployeeCodeMaxLength = 30;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 20;
    public const int PhotoPathMaxLength = 500;

    public Guid PublicId { get; private set; } = Guid.NewGuid();

    public required string EmployeeCode { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public required DateOnly DateOfBirth { get; set; }

    public required Gender Gender { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public required DateOnly JoiningDate { get; set; }

    public DateOnly? ConfirmationDate { get; set; }

    public DateOnly? SeparationDate { get; set; }

    public required EmploymentType EmploymentType { get; set; }

    public bool IsActive { get; set; } = true;

    public string? PhotoPath { get; set; }

    public long DepartmentId { get; set; }

    public Department Department { get; set; } = null!;

    public long DesignationId { get; set; }

    public Designation Designation { get; set; } = null!;

    public long WorkLocationId { get; set; }

    public WorkLocation WorkLocation { get; set; } = null!;

    public long? ReportingManagerId { get; set; }

    public Employee? ReportingManager { get; set; }

    /// <summary>
    /// The shift the employee works unless a roster entry says otherwise (FR-2.3). Null until one is
    /// assigned; processing then has no shift to measure the day against.
    /// </summary>
    public long? DefaultShiftId { get; set; }

    public Shift? DefaultShift { get; set; }

    /// <summary>The employee's regular weekly offs (FR-2.2). Null means none: every day is a working day unless rostered off.</summary>
    public long? WeeklyOffPatternId { get; set; }

    public WeeklyOffPattern? WeeklyOffPattern { get; set; }

    /// <summary>Display name. Not stored.</summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Records the employee's separation (FR-1.5). Nothing is deleted: the record and all of its history
    /// stay, and the employee drops out of lists filtered to active staff.
    /// </summary>
    /// <exception cref="InvalidOperationException">The employee is already inactive, or the date is before joining.</exception>
    public void Deactivate(DateOnly separationDate)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("The employee is already inactive.");
        }

        if (separationDate < JoiningDate)
        {
            throw new InvalidOperationException("The separation date can't be before the joining date.");
        }

        IsActive = false;
        SeparationDate = separationDate;
    }

    /// <summary>Undoes <see cref="Deactivate"/>, e.g. after a mistaken separation or a rehire.</summary>
    /// <exception cref="InvalidOperationException">The employee is already active.</exception>
    public void Reactivate()
    {
        if (IsActive)
        {
            throw new InvalidOperationException("The employee is already active.");
        }

        IsActive = true;
        SeparationDate = null;
    }
}
