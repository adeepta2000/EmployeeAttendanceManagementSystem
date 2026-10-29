using Attendance.Application.Common.Messaging;
using Attendance.Application.Common.Security;
using Attendance.Application.Organization.Abstractions;
using Attendance.Application.Organization.Employees;
using Attendance.Application.Scheduling.Abstractions;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Paging;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Scheduling.Rosters;

/// <summary>One roster entry, if its employee is in the caller's scope (FR-8.3); NotFound otherwise.</summary>
public sealed record GetRosterEntryQuery(Guid PublicId) : IQuery<Result<RosterEntryDto>>;

/// <summary>
/// A page of the roster entries in the caller's scope over a date range of up to
/// <see cref="ScheduleLimits.MaxRangeDays"/> days, by date and then employee code.
/// </summary>
/// <param name="EmployeeId">PublicId of an employee; entries of one outside the caller's scope never match.</param>
/// <param name="DepartmentId">PublicId of a department: the entries of its current employees.</param>
public sealed record ListRosterEntriesQuery(
    DateOnly FromDate,
    DateOnly ToDate,
    Guid? EmployeeId = null,
    Guid? DepartmentId = null,
    int Page = 1,
    int PageSize = ListRosterEntriesQuery.DefaultPageSize)
    : IQuery<PagedResult<RosterEntryDto>>
{
    public const int DefaultPageSize = 100;

    // A department's month is a few thousand small rows; a larger page means fewer round trips for a grid.
    public const int MaxPageSize = 500;
    public const int MaxPage = 100_000;
}

/// <summary>
/// The team roster grid (screen 16): a page of the active employees in the caller's scope, by employee
/// code, each with how every date from <see cref="FromDate"/> to <see cref="ToDate"/> resolves for them
/// (<see cref="ICalendarService"/>). A manager sees their reporting tree, HR the whole company (FR-8.3).
/// </summary>
/// <param name="DepartmentId">PublicId of a department: only its own employees.</param>
public sealed record ListTeamCalendarQuery(
    DateOnly FromDate,
    DateOnly ToDate,
    Guid? DepartmentId = null,
    int Page = 1,
    int PageSize = ListTeamCalendarQuery.DefaultPageSize)
    : IQuery<PagedResult<TeamCalendarRowDto>>
{
    public const int DefaultPageSize = 50;

    // Rows times days: 100 employees over 92 days is the largest page, about 9,000 calendar days.
    public const int MaxPageSize = ListEmployeesQuery.MaxPageSize;
}

internal sealed class ListTeamCalendarQueryValidator : AbstractValidator<ListTeamCalendarQuery>
{
    public ListTeamCalendarQueryValidator()
    {
        RuleFor(q => q.FromDate).MustBeScheduleDate();
        RuleFor(q => q.ToDate).MustBeScheduleDate().MustEndWithinRange(q => q.FromDate);
        RuleFor(q => q.DepartmentId).NotEqual(Guid.Empty);
        RuleFor(q => q.Page).InclusiveBetween(1, ListEmployeesQuery.MaxPage);
        RuleFor(q => q.PageSize).InclusiveBetween(1, ListTeamCalendarQuery.MaxPageSize);
    }
}

internal sealed class ListTeamCalendarQueryHandler(
    IEmployeeRepository employees,
    ICalendarRepository calendars,
    EmployeeScopeResolver scopes)
    : IQueryHandler<ListTeamCalendarQuery, PagedResult<TeamCalendarRowDto>>
{
    public async Task<PagedResult<TeamCalendarRowDto>> HandleAsync(ListTeamCalendarQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var page = await employees.ListAsync(
            new ListEmployeesQuery(DepartmentId: query.DepartmentId, IsActive: true, Page: query.Page, PageSize: query.PageSize),
            scope,
            cancellationToken);

        // One load for the whole page: a fixed handful of queries however many rows and days it holds.
        var calendar = new CalendarService(
            await calendars.LoadAsync([.. page.Items.Select(e => e.Id)], query.FromDate, query.ToDate, cancellationToken));
        var dates = Enumerable.Range(0, ScheduleLimits.DayCount(query.FromDate, query.ToDate)).Select(query.FromDate.AddDays).ToList();

        return page.Map(employee =>
            employee.ToTeamCalendarRow([.. dates.Select(date => calendar.GetDay(employee.Id, date).ToDto())]));
    }
}

internal sealed class ListRosterEntriesQueryValidator : AbstractValidator<ListRosterEntriesQuery>
{
    public ListRosterEntriesQueryValidator()
    {
        RuleFor(q => q.FromDate).MustBeScheduleDate();
        RuleFor(q => q.ToDate).MustBeScheduleDate().MustEndWithinRange(q => q.FromDate);
        RuleFor(q => q.EmployeeId).NotEqual(Guid.Empty);
        RuleFor(q => q.DepartmentId).NotEqual(Guid.Empty);
        RuleFor(q => q.Page).InclusiveBetween(1, ListRosterEntriesQuery.MaxPage);
        RuleFor(q => q.PageSize).InclusiveBetween(1, ListRosterEntriesQuery.MaxPageSize);
    }
}

internal sealed class GetRosterEntryQueryHandler(IRosterRepository rosters, EmployeeScopeResolver scopes)
    : IQueryHandler<GetRosterEntryQuery, Result<RosterEntryDto>>
{
    public async Task<Result<RosterEntryDto>> HandleAsync(GetRosterEntryQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        return await rosters.FindAsync(query.PublicId, scope, cancellationToken) is { } roster
            ? roster.ToDto()
            : RosterErrors.NotFound;
    }
}

internal sealed class ListRosterEntriesQueryHandler(IRosterRepository rosters, EmployeeScopeResolver scopes)
    : IQueryHandler<ListRosterEntriesQuery, PagedResult<RosterEntryDto>>
{
    public async Task<PagedResult<RosterEntryDto>> HandleAsync(ListRosterEntriesQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var page = await rosters.ListAsync(query, scope, cancellationToken);
        return page.Map(r => r.ToDto());
    }
}
