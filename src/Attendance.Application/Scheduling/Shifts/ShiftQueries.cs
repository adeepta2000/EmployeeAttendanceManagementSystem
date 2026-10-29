using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization.Abstractions;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Results;

namespace Attendance.Application.Scheduling.Shifts;

/// <param name="IsActive">Only active (true) or retired (false) shifts; null for all.</param>
public sealed record ListShiftsQuery(bool? IsActive) : IQuery<IReadOnlyList<ShiftDto>>;

public sealed record GetShiftQuery(Guid PublicId) : IQuery<Result<ShiftDto>>;

internal sealed class ListShiftsQueryHandler(IMasterDataRepository<Shift> shifts)
    : IQueryHandler<ListShiftsQuery, IReadOnlyList<ShiftDto>>
{
    public async Task<IReadOnlyList<ShiftDto>> HandleAsync(ListShiftsQuery query, CancellationToken cancellationToken) =>
        [.. (await shifts.ListAsync(query.IsActive, cancellationToken)).Select(s => s.ToDto())];
}

internal sealed class GetShiftQueryHandler(IMasterDataRepository<Shift> shifts)
    : IQueryHandler<GetShiftQuery, Result<ShiftDto>>
{
    public async Task<Result<ShiftDto>> HandleAsync(GetShiftQuery query, CancellationToken cancellationToken) =>
        await shifts.FindAsync(query.PublicId, cancellationToken) is { } shift
            ? shift.ToDto()
            : ShiftErrors.NotFound;
}
