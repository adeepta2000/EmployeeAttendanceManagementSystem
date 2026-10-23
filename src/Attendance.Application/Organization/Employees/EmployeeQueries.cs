using Attendance.Application.Common.Messaging;
using Attendance.Application.Common.Security;
using Attendance.Application.Organization.Abstractions;
using Attendance.Domain.Organization;
using Attendance.Shared.Paging;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Organization.Employees;

/// <summary>One employee, if the caller's scope includes them (FR-8.3); NotFound otherwise.</summary>
public sealed record GetEmployeeQuery(Guid PublicId) : IQuery<Result<EmployeeDto>>;

/// <summary>
/// A page of the employees in the caller's scope (FR-8.3), filtered and sorted (screen 26). Every
/// filter is optional and they combine with AND.
/// </summary>
/// <param name="Search">Matches part of the employee code, first name, last name or full name.</param>
/// <param name="DepartmentId">PublicId of a department.</param>
/// <param name="DesignationId">PublicId of a designation.</param>
/// <param name="WorkLocationId">PublicId of a work location.</param>
/// <param name="IsActive">Only active (true) or deactivated (false) employees; null for both.</param>
/// <param name="Page">1-based.</param>
public sealed record ListEmployeesQuery(
    string? Search = null,
    Guid? DepartmentId = null,
    Guid? DesignationId = null,
    Guid? WorkLocationId = null,
    EmploymentType? EmploymentType = null,
    bool? IsActive = null,
    EmployeeSortField SortBy = EmployeeSortField.EmployeeCode,
    SortDirection SortDirection = SortDirection.Asc,
    int Page = 1,
    int PageSize = ListEmployeesQuery.DefaultPageSize)
    : IQuery<PagedResult<EmployeeSummaryDto>>
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int SearchMaxLength = 100;

    // Keeps OFFSET within reach of any real list while ruling out arithmetic overflow.
    public const int MaxPage = 100_000;
}

public enum EmployeeSortField
{
    EmployeeCode,

    /// <summary>First name, then last name.</summary>
    Name,
    JoiningDate,

    /// <summary>Department name.</summary>
    Department,

    /// <summary>Designation name.</summary>
    Designation,
}

internal sealed class ListEmployeesQueryValidator : AbstractValidator<ListEmployeesQuery>
{
    public ListEmployeesQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(ListEmployeesQuery.SearchMaxLength);
        RuleFor(q => q.DepartmentId).NotEqual(Guid.Empty);
        RuleFor(q => q.DesignationId).NotEqual(Guid.Empty);
        RuleFor(q => q.WorkLocationId).NotEqual(Guid.Empty);
        RuleFor(q => q.EmploymentType).IsInEnum();
        RuleFor(q => q.SortBy).IsInEnum();
        RuleFor(q => q.SortDirection).IsInEnum();
        RuleFor(q => q.Page).InclusiveBetween(1, ListEmployeesQuery.MaxPage);
        RuleFor(q => q.PageSize).InclusiveBetween(1, ListEmployeesQuery.MaxPageSize);
    }
}

internal sealed class GetEmployeeQueryHandler(IEmployeeRepository employees, EmployeeScopeResolver scopes)
    : IQueryHandler<GetEmployeeQuery, Result<EmployeeDto>>
{
    public async Task<Result<EmployeeDto>> HandleAsync(GetEmployeeQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var employee = await employees.FindAsync(query.PublicId, scope, cancellationToken);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        return employee.ToDto(await employees.HasUserAccountAsync(employee.Id, cancellationToken));
    }
}

internal sealed class ListEmployeesQueryHandler(IEmployeeRepository employees, EmployeeScopeResolver scopes)
    : IQueryHandler<ListEmployeesQuery, PagedResult<EmployeeSummaryDto>>
{
    public async Task<PagedResult<EmployeeSummaryDto>> HandleAsync(ListEmployeesQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var page = await employees.ListAsync(query, scope, cancellationToken);
        return page.Map(e => e.ToSummaryDto());
    }
}
