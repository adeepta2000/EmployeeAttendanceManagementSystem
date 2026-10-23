using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization.Abstractions;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;

namespace Attendance.Application.Organization.Designations;

/// <param name="IsActive">Only active (true) or retired (false) designations; null for all.</param>
public sealed record ListDesignationsQuery(bool? IsActive) : IQuery<IReadOnlyList<DesignationDto>>;

public sealed record GetDesignationQuery(Guid PublicId) : IQuery<Result<DesignationDto>>;

internal sealed class ListDesignationsQueryHandler(IMasterDataRepository<Designation> designations)
    : IQueryHandler<ListDesignationsQuery, IReadOnlyList<DesignationDto>>
{
    public async Task<IReadOnlyList<DesignationDto>> HandleAsync(ListDesignationsQuery query, CancellationToken cancellationToken) =>
        [.. (await designations.ListAsync(query.IsActive, cancellationToken)).Select(d => d.ToDto())];
}

internal sealed class GetDesignationQueryHandler(IMasterDataRepository<Designation> designations)
    : IQueryHandler<GetDesignationQuery, Result<DesignationDto>>
{
    public async Task<Result<DesignationDto>> HandleAsync(GetDesignationQuery query, CancellationToken cancellationToken) =>
        await designations.FindAsync(query.PublicId, cancellationToken) is { } designation
            ? designation.ToDto()
            : DesignationErrors.NotFound;
}
