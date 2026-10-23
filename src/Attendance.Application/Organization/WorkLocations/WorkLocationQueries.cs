using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization.Abstractions;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;

namespace Attendance.Application.Organization.WorkLocations;

/// <param name="IsActive">Only active (true) or retired (false) locations; null for all.</param>
public sealed record ListWorkLocationsQuery(bool? IsActive) : IQuery<IReadOnlyList<WorkLocationDto>>;

public sealed record GetWorkLocationQuery(Guid PublicId) : IQuery<Result<WorkLocationDto>>;

internal sealed class ListWorkLocationsQueryHandler(IMasterDataRepository<WorkLocation> workLocations)
    : IQueryHandler<ListWorkLocationsQuery, IReadOnlyList<WorkLocationDto>>
{
    public async Task<IReadOnlyList<WorkLocationDto>> HandleAsync(ListWorkLocationsQuery query, CancellationToken cancellationToken) =>
        [.. (await workLocations.ListAsync(query.IsActive, cancellationToken)).Select(w => w.ToDto())];
}

internal sealed class GetWorkLocationQueryHandler(IMasterDataRepository<WorkLocation> workLocations)
    : IQueryHandler<GetWorkLocationQuery, Result<WorkLocationDto>>
{
    public async Task<Result<WorkLocationDto>> HandleAsync(GetWorkLocationQuery query, CancellationToken cancellationToken) =>
        await workLocations.FindAsync(query.PublicId, cancellationToken) is { } location
            ? location.ToDto()
            : WorkLocationErrors.NotFound;
}
