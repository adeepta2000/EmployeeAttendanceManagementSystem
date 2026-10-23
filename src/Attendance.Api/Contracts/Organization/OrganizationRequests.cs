using Attendance.Application.Organization.Departments;
using Attendance.Application.Organization.Designations;
using Attendance.Application.Organization.Employees;
using Attendance.Application.Organization.WorkLocations;
using Attendance.Domain.Organization;
using Attendance.Shared.Paging;

namespace Attendance.Api.Contracts.Organization;

// Request bodies are separate from the commands: the PublicId of an update comes from the route, and
// binding the command directly would let the body set it. References to other records are PublicIds.

/// <param name="ParentDepartmentId">PublicId of the parent department, if any.</param>
/// <param name="HeadEmployeeId">PublicId of the employee who heads the department, if any.</param>
public sealed record DepartmentRequest(string Code, string Name, Guid? ParentDepartmentId, Guid? HeadEmployeeId)
{
    public CreateDepartmentCommand ToCreateCommand() => new(Code, Name, ParentDepartmentId, HeadEmployeeId);

    public UpdateDepartmentCommand ToUpdateCommand(Guid publicId) => new(publicId, Code, Name, ParentDepartmentId, HeadEmployeeId);
}

public sealed record DesignationRequest(string Code, string Name, string? Grade)
{
    public CreateDesignationCommand ToCreateCommand() => new(Code, Name, Grade);

    public UpdateDesignationCommand ToUpdateCommand(Guid publicId) => new(publicId, Code, Name, Grade);
}

/// <param name="GeofenceRadiusMeters">Punches are accepted within this distance of the coordinates (FR-3.2). Optional.</param>
public sealed record WorkLocationRequest(
    string Code,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters)
{
    public CreateWorkLocationCommand ToCreateCommand() =>
        new(Code, Name, Address, Latitude, Longitude, GeofenceRadiusMeters);

    public UpdateWorkLocationCommand ToUpdateCommand(Guid publicId) =>
        new(publicId, Code, Name, Address, Latitude, Longitude, GeofenceRadiusMeters);
}

/// <param name="DepartmentId">PublicId of an active department.</param>
/// <param name="DesignationId">PublicId of an active designation.</param>
/// <param name="WorkLocationId">PublicId of an active work location.</param>
/// <param name="ReportingManagerId">PublicId of an active employee within your scope; omit for none.</param>
public sealed record EmployeeRequest(
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
{
    public CreateEmployeeCommand ToCreateCommand() => new(
        EmployeeCode, FirstName, LastName, DateOfBirth, Gender, Email, Phone, JoiningDate, ConfirmationDate,
        EmploymentType, DepartmentId, DesignationId, WorkLocationId, ReportingManagerId);

    public UpdateEmployeeCommand ToUpdateCommand(Guid publicId) => new(
        publicId, EmployeeCode, FirstName, LastName, DateOfBirth, Gender, Email, Phone, JoiningDate, ConfirmationDate,
        EmploymentType, DepartmentId, DesignationId, WorkLocationId, ReportingManagerId);

    // Keeps personal data out of logs and exception messages.
    public override string ToString() => nameof(EmployeeRequest);
}

public sealed record DeactivateEmployeeRequest(DateOnly SeparationDate);

/// <summary>Query-string parameters of <c>GET /api/employees</c>. Every filter is optional.</summary>
public sealed class EmployeeListRequest
{
    /// <summary>Part of the employee code, first name, last name or full name.</summary>
    public string? Search { get; init; }

    /// <summary>PublicId of a department.</summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>PublicId of a designation.</summary>
    public Guid? DesignationId { get; init; }

    /// <summary>PublicId of a work location.</summary>
    public Guid? WorkLocationId { get; init; }

    public EmploymentType? EmploymentType { get; init; }

    /// <summary>True for active employees only, false for deactivated ones only; omit for both.</summary>
    public bool? IsActive { get; init; }

    public EmployeeSortField SortBy { get; init; } = EmployeeSortField.EmployeeCode;

    public SortDirection SortDirection { get; init; } = SortDirection.Asc;

    /// <summary>1-based.</summary>
    public int Page { get; init; } = 1;

    /// <summary>At most 100.</summary>
    public int PageSize { get; init; } = ListEmployeesQuery.DefaultPageSize;

    public ListEmployeesQuery ToQuery() => new(
        Search, DepartmentId, DesignationId, WorkLocationId, EmploymentType, IsActive, SortBy, SortDirection, Page, PageSize);
}
