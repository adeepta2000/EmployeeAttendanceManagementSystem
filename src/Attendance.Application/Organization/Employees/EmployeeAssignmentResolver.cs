using Attendance.Application.Common.Security;
using Attendance.Application.Organization.Abstractions;
using Attendance.Domain.Common;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;

namespace Attendance.Application.Organization.Employees;

/// <summary>The resolved organization links of an employee.</summary>
internal sealed record EmployeeAssignment(
    Department Department,
    Designation Designation,
    WorkLocation WorkLocation,
    Employee? ReportingManager);

/// <summary>Resolves and checks the department, designation, work location and manager named by a create or update.</summary>
internal sealed class EmployeeAssignmentResolver(
    IDepartmentRepository departments,
    IMasterDataRepository<Designation> designations,
    IMasterDataRepository<WorkLocation> workLocations,
    IEmployeeRepository employees)
{
    /// <param name="employee">The employee being updated; null when creating one.</param>
    /// <param name="scope">The caller's scope. A new reporting manager must be inside it (FR-8.3).</param>
    public async Task<Result<EmployeeAssignment>> ResolveAsync(
        IEmployeeAssignment input,
        Employee? employee,
        EmployeeScope scope,
        CancellationToken cancellationToken)
    {
        var department = await departments.FindAsync(input.DepartmentId, cancellationToken);
        if (!IsUsable(department, employee?.DepartmentId))
        {
            return EmployeeErrors.DepartmentNotFound;
        }

        var designation = await designations.FindAsync(input.DesignationId, cancellationToken);
        if (!IsUsable(designation, employee?.DesignationId))
        {
            return EmployeeErrors.DesignationNotFound;
        }

        var workLocation = await workLocations.FindAsync(input.WorkLocationId, cancellationToken);
        if (!IsUsable(workLocation, employee?.WorkLocationId))
        {
            return EmployeeErrors.WorkLocationNotFound;
        }

        Employee? manager = null;
        if (input.ReportingManagerId is { } managerId)
        {
            if (employee?.ReportingManager?.PublicId == managerId)
            {
                manager = employee.ReportingManager;
            }
            else
            {
                manager = await employees.FindAsync(managerId, scope, cancellationToken);
                if (manager is not { IsActive: true })
                {
                    return EmployeeErrors.ManagerNotFound;
                }

                if (employee is not null && await employees.IsInManagementChainAsync(employee.Id, manager.Id, cancellationToken))
                {
                    return EmployeeErrors.ManagerCycle;
                }
            }
        }

        return new EmployeeAssignment(department!, designation!, workLocation!, manager);
    }

    // A retired row is still acceptable when the employee already points at it, so that editing an old
    // record doesn't force a transfer first.
    private static bool IsUsable<T>(T? row, long? currentId)
        where T : BaseEntity, IMasterData =>
        row is not null && (row.IsActive || row.Id == currentId);
}

internal static class EmployeeAssignmentExtensions
{
    public static void Assign(this Employee employee, EmployeeAssignment assignment)
    {
        employee.Department = assignment.Department;
        employee.DepartmentId = assignment.Department.Id;
        employee.Designation = assignment.Designation;
        employee.DesignationId = assignment.Designation.Id;
        employee.WorkLocation = assignment.WorkLocation;
        employee.WorkLocationId = assignment.WorkLocation.Id;
        employee.ReportingManager = assignment.ReportingManager;
        employee.ReportingManagerId = assignment.ReportingManager?.Id;
    }
}
