using Attendance.Domain.Organization;

namespace Attendance.Application.Organization;

// Response DTOs. Records are identified by PublicId only; internal
// IDs never leave the server. Those carrying personal data override ToString to keep it out of logs.

/// <summary>A department, designation or work location as referenced from another record.</summary>
public sealed record OrgUnitRef(Guid PublicId, string Code, string Name);

/// <summary>An employee as referenced from another record, e.g. a reporting manager or department head.</summary>
public sealed record EmployeeRef(Guid PublicId, string EmployeeCode, string FullName)
{
    public override string ToString() => $"{nameof(EmployeeRef)} {{ PublicId = {PublicId} }}";
}

public sealed record DepartmentDto(
    Guid PublicId,
    string Code,
    string Name,
    OrgUnitRef? ParentDepartment,
    EmployeeRef? HeadEmployee,
    bool IsActive);

public sealed record DesignationDto(Guid PublicId, string Code, string Name, string? Grade, bool IsActive);

public sealed record WorkLocationDto(
    Guid PublicId,
    string Code,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters,
    bool IsActive);

/// <summary>An employee as a list row (screen 26).</summary>
public sealed record EmployeeSummaryDto(
    Guid PublicId,
    string EmployeeCode,
    string FirstName,
    string LastName,
    string FullName,
    OrgUnitRef Department,
    OrgUnitRef Designation,
    OrgUnitRef WorkLocation,
    EmploymentType EmploymentType,
    EmployeeRef? ReportingManager,
    DateOnly JoiningDate,
    bool IsActive)
{
    public override string ToString() => $"{nameof(EmployeeSummaryDto)} {{ PublicId = {PublicId} }}";
}

/// <summary>The full employee record (FR-1.2, screen 27).</summary>
/// <param name="HasUserAccount">Whether the employee can sign in. Floor workers who only punch on a device have no account (FR-1.3).</param>
public sealed record EmployeeDto(
    Guid PublicId,
    string EmployeeCode,
    string FirstName,
    string LastName,
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? Email,
    string? Phone,
    OrgUnitRef Department,
    OrgUnitRef Designation,
    OrgUnitRef WorkLocation,
    EmploymentType EmploymentType,
    EmployeeRef? ReportingManager,
    DateOnly JoiningDate,
    DateOnly? ConfirmationDate,
    DateOnly? SeparationDate,
    bool IsActive,
    bool HasUserAccount)
{
    public override string ToString() => $"{nameof(EmployeeDto)} {{ PublicId = {PublicId} }}";
}
