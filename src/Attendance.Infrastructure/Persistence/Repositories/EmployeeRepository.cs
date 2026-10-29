using Attendance.Application.Common.Security;
using Attendance.Application.Organization.Abstractions;
using Attendance.Application.Organization.Employees;
using Attendance.Domain.Organization;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Paging;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence.Repositories;

internal sealed class EmployeeRepository(AttendanceDbContext db) : IEmployeeRepository
{
    public Task<Employee?> FindAsync(Guid publicId, EmployeeScope scope, CancellationToken cancellationToken) =>
        WithAssignments(db.Employees)
            .InScope(scope)
            .SingleOrDefaultAsync(e => e.PublicId == publicId, cancellationToken);

    public async Task<PagedResult<Employee>> ListAsync(ListEmployeesQuery query, EmployeeScope scope, CancellationToken cancellationToken)
    {
        var employees = db.Employees.AsNoTracking().InScope(scope);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Contains becomes a parameterized LIKE with wildcards in the term escaped.
            var term = query.Search.Trim();
            employees = employees.Where(e =>
                e.EmployeeCode.Contains(term)
                || e.FirstName.Contains(term)
                || e.LastName.Contains(term)
                || (e.FirstName + " " + e.LastName).Contains(term));
        }

        if (query.DepartmentId is { } departmentId)
        {
            employees = employees.Where(e => e.Department.PublicId == departmentId);
        }

        if (query.DesignationId is { } designationId)
        {
            employees = employees.Where(e => e.Designation.PublicId == designationId);
        }

        if (query.WorkLocationId is { } workLocationId)
        {
            employees = employees.Where(e => e.WorkLocation.PublicId == workLocationId);
        }

        if (query.EmploymentType is { } employmentType)
        {
            employees = employees.Where(e => e.EmploymentType == employmentType);
        }

        if (query.IsActive is { } isActive)
        {
            employees = employees.Where(e => e.IsActive == isActive);
        }

        var totalCount = await employees.CountAsync(cancellationToken);
        var items = await WithAssignments(Sort(employees, query.SortBy, query.SortDirection))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Employee>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<long>> FindActiveIdsAsync(
        IReadOnlyCollection<Guid> publicIds, EmployeeScope scope, CancellationToken cancellationToken)
    {
        if (publicIds.Count == 0)
        {
            return [];
        }

        var ids = publicIds.ToArray();
        return await db.Employees
            .InScope(scope)
            .Where(e => e.IsActive && EF.Parameter(ids).Contains(e.PublicId))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string employeeCode, CancellationToken cancellationToken) =>
        db.Employees.AnyAsync(e => e.EmployeeCode == employeeCode, cancellationToken);

    public async Task<IReadOnlyList<EmployeeCodeInfo>> FindByCodesAsync(
        IReadOnlyCollection<string> employeeCodes, CancellationToken cancellationToken)
    {
        if (employeeCodes.Count == 0)
        {
            return [];
        }

        // One JSON parameter rather than one SQL parameter per code: an import can hold thousands.
        var codes = employeeCodes.ToArray();
        return await db.Employees
            .Where(e => EF.Parameter(codes).Contains(e.EmployeeCode))
            .Select(e => new EmployeeCodeInfo(e.Id, e.EmployeeCode, e.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsInManagementChainAsync(long employeeId, long managerId, CancellationToken cancellationToken)
    {
        // Walks up one level per query. The visited set ends the walk if bad data already holds a loop.
        var visited = new HashSet<long>();
        for (long? current = managerId; current is { } id && visited.Add(id);)
        {
            if (id == employeeId)
            {
                return true;
            }

            current = await db.Employees
                .Where(e => e.Id == id)
                .Select(e => e.ReportingManagerId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return false;
    }

    public Task<bool> HasUserAccountAsync(long employeeId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.EmployeeId == employeeId, cancellationToken);

    public Task<bool> AnyActiveInDepartmentAsync(long departmentId, CancellationToken cancellationToken) =>
        db.Employees.AnyAsync(e => e.DepartmentId == departmentId && e.IsActive, cancellationToken);

    public Task<bool> AnyActiveWithDesignationAsync(long designationId, CancellationToken cancellationToken) =>
        db.Employees.AnyAsync(e => e.DesignationId == designationId && e.IsActive, cancellationToken);

    public Task<bool> AnyActiveAtWorkLocationAsync(long workLocationId, CancellationToken cancellationToken) =>
        db.Employees.AnyAsync(e => e.WorkLocationId == workLocationId && e.IsActive, cancellationToken);

    public Task<bool> AnyActiveWithDefaultShiftAsync(long shiftId, CancellationToken cancellationToken) =>
        db.Employees.AnyAsync(e => e.DefaultShiftId == shiftId && e.IsActive, cancellationToken);

    public Task<bool> AnyActiveWithWeeklyOffPatternAsync(long patternId, CancellationToken cancellationToken) =>
        db.Employees.AnyAsync(e => e.WeeklyOffPatternId == patternId && e.IsActive, cancellationToken);

    public void Add(Employee employee) => db.Employees.Add(employee);

    private static IQueryable<Employee> WithAssignments(IQueryable<Employee> employees) =>
        employees
            .Include(e => e.Department)
            .Include(e => e.Designation)
            .Include(e => e.WorkLocation)
            .Include(e => e.ReportingManager)
            .Include(e => e.DefaultShift)
            .Include(e => e.WeeklyOffPattern);

    // The Id tiebreaker keeps paging stable when the sort key has duplicates.
    private static IQueryable<Employee> Sort(IQueryable<Employee> employees, EmployeeSortField sortBy, SortDirection direction) =>
        (sortBy, direction) switch
        {
            (EmployeeSortField.Name, SortDirection.Asc) => employees.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ThenBy(e => e.Id),
            (EmployeeSortField.Name, _) => employees.OrderByDescending(e => e.FirstName).ThenByDescending(e => e.LastName).ThenBy(e => e.Id),
            (EmployeeSortField.JoiningDate, SortDirection.Asc) => employees.OrderBy(e => e.JoiningDate).ThenBy(e => e.Id),
            (EmployeeSortField.JoiningDate, _) => employees.OrderByDescending(e => e.JoiningDate).ThenBy(e => e.Id),
            (EmployeeSortField.Department, SortDirection.Asc) => employees.OrderBy(e => e.Department.Name).ThenBy(e => e.EmployeeCode),
            (EmployeeSortField.Department, _) => employees.OrderByDescending(e => e.Department.Name).ThenBy(e => e.EmployeeCode),
            (EmployeeSortField.Designation, SortDirection.Asc) => employees.OrderBy(e => e.Designation.Name).ThenBy(e => e.EmployeeCode),
            (EmployeeSortField.Designation, _) => employees.OrderByDescending(e => e.Designation.Name).ThenBy(e => e.EmployeeCode),
            (_, SortDirection.Asc) => employees.OrderBy(e => e.EmployeeCode),
            _ => employees.OrderByDescending(e => e.EmployeeCode),
        };
}

internal static class EmployeeScopeQueryExtensions
{
    /// <summary>
    /// The data-scoping filter (FR-8.3), applied inside the SQL query so out-of-scope rows never leave the
    /// database. The IDs travel as one JSON parameter, so a large reporting tree can't hit SQL Server's
    /// 2,100-parameter limit.
    /// </summary>
    public static IQueryable<Employee> InScope(this IQueryable<Employee> employees, EmployeeScope scope)
    {
        if (scope.IsCompanyWide)
        {
            return employees;
        }

        var ids = scope.EmployeeIds.ToArray();
        return employees.Where(e => EF.Parameter(ids).Contains(e.Id));
    }

    /// <summary>The same filter for roster entries, applied to the entry's employee.</summary>
    public static IQueryable<EmployeeShiftRoster> InScope(this IQueryable<EmployeeShiftRoster> rosters, EmployeeScope scope)
    {
        if (scope.IsCompanyWide)
        {
            return rosters;
        }

        var ids = scope.EmployeeIds.ToArray();
        return rosters.Where(r => EF.Parameter(ids).Contains(r.EmployeeId));
    }
}
