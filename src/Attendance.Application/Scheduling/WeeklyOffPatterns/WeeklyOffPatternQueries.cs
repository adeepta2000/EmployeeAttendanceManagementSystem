using Attendance.Application.Common.Messaging;
using Attendance.Application.Scheduling.Abstractions;
using Attendance.Shared.Results;

namespace Attendance.Application.Scheduling.WeeklyOffPatterns;

/// <param name="IsActive">Only active (true) or retired (false) patterns; null for all.</param>
public sealed record ListWeeklyOffPatternsQuery(bool? IsActive) : IQuery<IReadOnlyList<WeeklyOffPatternDto>>;

public sealed record GetWeeklyOffPatternQuery(Guid PublicId) : IQuery<Result<WeeklyOffPatternDto>>;

internal sealed class ListWeeklyOffPatternsQueryHandler(IWeeklyOffPatternRepository patterns)
    : IQueryHandler<ListWeeklyOffPatternsQuery, IReadOnlyList<WeeklyOffPatternDto>>
{
    public async Task<IReadOnlyList<WeeklyOffPatternDto>> HandleAsync(ListWeeklyOffPatternsQuery query, CancellationToken cancellationToken) =>
        [.. (await patterns.ListAsync(query.IsActive, cancellationToken)).Select(p => p.ToDto())];
}

internal sealed class GetWeeklyOffPatternQueryHandler(IWeeklyOffPatternRepository patterns)
    : IQueryHandler<GetWeeklyOffPatternQuery, Result<WeeklyOffPatternDto>>
{
    public async Task<Result<WeeklyOffPatternDto>> HandleAsync(GetWeeklyOffPatternQuery query, CancellationToken cancellationToken) =>
        await patterns.FindAsync(query.PublicId, cancellationToken) is { } pattern
            ? pattern.ToDto()
            : WeeklyOffPatternErrors.NotFound;
}
