using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization.Abstractions;
using Attendance.Shared.Results;

namespace Attendance.Application.Organization.Departments;

/// <param name="IsActive">Only active (true) or retired (false) departments; null for all.</param>
public sealed record ListDepartmentsQuery(bool? IsActive) : IQuery<IReadOnlyList<DepartmentDto>>;

public sealed record GetDepartmentQuery(Guid PublicId) : IQuery<Result<DepartmentDto>>;

internal sealed class ListDepartmentsQueryHandler(IDepartmentRepository departments)
    : IQueryHandler<ListDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    public async Task<IReadOnlyList<DepartmentDto>> HandleAsync(ListDepartmentsQuery query, CancellationToken cancellationToken) =>
        [.. (await departments.ListAsync(query.IsActive, cancellationToken)).Select(d => d.ToDto())];
}

internal sealed class GetDepartmentQueryHandler(IDepartmentRepository departments)
    : IQueryHandler<GetDepartmentQuery, Result<DepartmentDto>>
{
    public async Task<Result<DepartmentDto>> HandleAsync(GetDepartmentQuery query, CancellationToken cancellationToken) =>
        await departments.FindAsync(query.PublicId, cancellationToken) is { } department
            ? department.ToDto()
            : DepartmentErrors.NotFound;
}
