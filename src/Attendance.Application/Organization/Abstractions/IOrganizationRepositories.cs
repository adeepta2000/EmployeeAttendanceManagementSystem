using Attendance.Application.Common.Security;
using Attendance.Application.Organization.Employees;
using Attendance.Domain.Common;
using Attendance.Domain.Organization;
using Attendance.Shared.Paging;

namespace Attendance.Application.Organization.Abstractions;

/// <summary>Departments, designations and work locations (FR-1.1).</summary>
public interface IMasterDataRepository<T>
    where T : class, IMasterData
{
    /// <summary>Finds a row by PublicId, active or not. Tracked.</summary>
    Task<T?> FindAsync(Guid publicId, CancellationToken cancellationToken);

    /// <summary>Every row, or only active or inactive ones, ordered by name. Not tracked.</summary>
    Task<IReadOnlyList<T>> ListAsync(bool? isActive, CancellationToken cancellationToken);

    /// <summary>True when any row, active or not, already uses <paramref name="code"/>.</summary>
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);

    /// <summary>Internal IDs of the active rows, keyed by code (case-insensitive), for resolving codes in bulk.</summary>
    Task<IReadOnlyDictionary<string, long>> GetActiveIdsByCodeAsync(CancellationToken cancellationToken);

    void Add(T entity);
}

/// <summary>Finds and lists load <see cref="Department.ParentDepartment"/> and <see cref="Department.HeadEmployee"/>.</summary>
public interface IDepartmentRepository : IMasterDataRepository<Department>
{
    /// <summary>The parent of every department, for walking the hierarchy without a query per level.</summary>
    Task<IReadOnlyDictionary<long, long?>> GetParentLinksAsync(CancellationToken cancellationToken);

    Task<bool> HasActiveChildrenAsync(long departmentId, CancellationToken cancellationToken);
}

public interface IEmployeeRepository
{
    /// <summary>
    /// The employee, if it exists and is inside <paramref name="scope"/>; otherwise null, so callers can't
    /// tell "outside your scope" from "doesn't exist". Tracked, with department, designation, work
    /// location and reporting manager loaded.
    /// </summary>
    Task<Employee?> FindAsync(Guid publicId, EmployeeScope scope, CancellationToken cancellationToken);

    /// <summary>One page of the employees inside <paramref name="scope"/> that match the query. Not tracked.</summary>
    Task<PagedResult<Employee>> ListAsync(ListEmployeesQuery query, EmployeeScope scope, CancellationToken cancellationToken);

    /// <summary>True when any employee, active or not and in any scope, already has the code.</summary>
    Task<bool> CodeExistsAsync(string employeeCode, CancellationToken cancellationToken);

    /// <summary>Every employee with one of the codes, in any scope. The caller applies the scope.</summary>
    Task<IReadOnlyList<EmployeeCodeInfo>> FindByCodesAsync(IReadOnlyCollection<string> employeeCodes, CancellationToken cancellationToken);

    /// <summary>
    /// True when <paramref name="employeeId"/> is <paramref name="managerId"/> or anyone above them in the
    /// reporting chain, i.e. when making them report to <paramref name="managerId"/> would close a loop.
    /// </summary>
    Task<bool> IsInManagementChainAsync(long employeeId, long managerId, CancellationToken cancellationToken);

    Task<bool> HasUserAccountAsync(long employeeId, CancellationToken cancellationToken);

    Task<bool> AnyActiveInDepartmentAsync(long departmentId, CancellationToken cancellationToken);

    Task<bool> AnyActiveWithDesignationAsync(long designationId, CancellationToken cancellationToken);

    Task<bool> AnyActiveAtWorkLocationAsync(long workLocationId, CancellationToken cancellationToken);

    void Add(Employee employee);
}

public sealed record EmployeeCodeInfo(long Id, string EmployeeCode, bool IsActive);
