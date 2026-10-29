using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization.Abstractions;
using Attendance.Application.Scheduling.Abstractions;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Scheduling.Holidays;

/// <summary>A year's holiday calendar, by date (FR-7.1, screen 32).</summary>
/// <param name="WorkLocationId">
/// PublicId of a work location: only the holidays that apply there, i.e. those limited to it and those
/// that apply everywhere. An unknown location gives an empty list.
/// </param>
public sealed record ListHolidaysQuery(int Year, Guid? WorkLocationId) : IQuery<IReadOnlyList<HolidayDto>>;

public sealed record GetHolidayQuery(Guid PublicId) : IQuery<Result<HolidayDto>>;

internal sealed class ListHolidaysQueryValidator : AbstractValidator<ListHolidaysQuery>
{
    public ListHolidaysQueryValidator()
    {
        RuleFor(q => q.Year).InclusiveBetween(ScheduleLimits.EarliestDate.Year, ScheduleLimits.LatestDate.Year);
        RuleFor(q => q.WorkLocationId).NotEqual(Guid.Empty);
    }
}

internal sealed class ListHolidaysQueryHandler(IHolidayRepository holidays, IMasterDataRepository<WorkLocation> workLocations)
    : IQueryHandler<ListHolidaysQuery, IReadOnlyList<HolidayDto>>
{
    public async Task<IReadOnlyList<HolidayDto>> HandleAsync(ListHolidaysQuery query, CancellationToken cancellationToken)
    {
        long? workLocationId = null;
        if (query.WorkLocationId is { } publicId)
        {
            if (await workLocations.FindAsync(publicId, cancellationToken) is not { } location)
            {
                return [];
            }

            workLocationId = location.Id;
        }

        return [.. (await holidays.ListAsync(query.Year, workLocationId, cancellationToken)).Select(h => h.ToDto())];
    }
}

internal sealed class GetHolidayQueryHandler(IHolidayRepository holidays)
    : IQueryHandler<GetHolidayQuery, Result<HolidayDto>>
{
    public async Task<Result<HolidayDto>> HandleAsync(GetHolidayQuery query, CancellationToken cancellationToken) =>
        await holidays.FindAsync(query.PublicId, cancellationToken) is { } holiday
            ? holiday.ToDto()
            : HolidayErrors.NotFound;
}
