using Attendance.Domain.Organization;
using Riok.Mapperly.Abstractions;

namespace Attendance.Application.Organization;

/// <summary>
/// Entity-to-DTO mappings, generated at compile time by Mapperly. Every DTO member must have a source
/// (a build error otherwise); entity members a DTO leaves out, such as the internal Id, are simply not copied.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class OrganizationMapper
{
    public static partial DepartmentDto ToDto(this Department department);

    public static partial DesignationDto ToDto(this Designation designation);

    public static partial WorkLocationDto ToDto(this WorkLocation workLocation);

    /// <remarks>Needs <see cref="Employee.Department"/>, <see cref="Employee.Designation"/>, <see cref="Employee.WorkLocation"/> and <see cref="Employee.ReportingManager"/> loaded.</remarks>
    public static partial EmployeeSummaryDto ToSummaryDto(this Employee employee);

    /// <inheritdoc cref="ToSummaryDto" />
    public static partial EmployeeDto ToDto(this Employee employee, bool hasUserAccount);

    public static partial EmployeeRef ToRef(this Employee employee);
}
