using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Application.Scheduling;
using Attendance.Domain.Scheduling;
using Attendance.Infrastructure.Persistence.Seeding;
using Attendance.Shared.Paging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Tests.Scheduling;

/// <summary>
/// The team roster grid (screen 16): reading a page of employees with their resolved calendars, and
/// assigning or clearing a drag-selected block of employees × dates in one request. Dates are in
/// 2033, a year no other test class adds holidays to.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RosterSelectionApiTests(AttendanceApiFactory api)
{
    private static readonly DateOnly From = new(2033, 3, 1);
    private static readonly DateOnly To = new(2033, 3, 7);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_team_calendar_lists_active_employees_of_the_department_with_every_date_resolved()
    {
        var org = await api.CreateOrgAsync();
        var rafiq = await api.CreateEmployeeAsync(org);
        var joiner = await api.CreateEmployeeAsync(org, joiningDate: From.AddDays(3));
        var leaver = await api.CreateEmployeeAsync(org, isActive: false);
        var elsewhere = await api.CreateEmployeeAsync(await api.CreateOrgAsync());
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var general = await hrManager.CreateShiftAsync();
        var night = await hrManager.CreateShiftAsync("22:00:00", "06:00:00");
        var pattern = await hrManager.CreateFridayAndAlternateSaturdaysAsync();
        await hrManager.SetScheduleAsync(rafiq, general.PublicId, pattern.PublicId);
        (await hrManager.PostAsync("/api/rosters", new { employeeId = rafiq.PublicId, rosterDate = From, shiftId = night.PublicId }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var page = await TeamCalendarAsync(hrManager, org);

        page.TotalCount.Should().Be(2, "the leaver and the employee of another department aren't listed");
        page.Items.Select(r => r.Employee.PublicId).Should().BeEquivalentTo([rafiq.PublicId, joiner.PublicId]);
        page.Items.Should().NotContain(r => r.Employee.PublicId == leaver.PublicId || r.Employee.PublicId == elsewhere.PublicId);

        var row = page.Items.Single(r => r.Employee.PublicId == rafiq.PublicId);
        row.Department.PublicId.Should().Be(org.Department.PublicId);
        row.DefaultShift.Should().Be(new ShiftRef(general.PublicId, general.Code, general.Name));
        row.WeeklyOffPattern!.PublicId.Should().Be(pattern.PublicId);
        row.Days.Select(d => d.Date).Should().Equal(Enumerable.Range(0, 7).Select(From.AddDays));
        row.Days[0].Should().Be(new CalendarDayDto(From, DayType.Working, new ShiftRef(night.PublicId, night.Code, night.Name), true, null));
        // 4 March 2033 is a Friday.
        row.Days[3].DayType.Should().Be(DayType.WeeklyOff);
        row.Days[1].Should().Be(new CalendarDayDto(From.AddDays(1), DayType.Working, row.DefaultShift, false, null));

        page.Items.Single(r => r.Employee.PublicId == joiner.PublicId).JoiningDate.Should().Be(From.AddDays(3));
    }

    [Fact]
    public async Task A_manager_sees_only_their_reporting_tree_and_a_range_over_92_days_is_refused()
    {
        var org = await api.CreateOrgAsync();
        var nusrat = await api.CreateEmployeeAsync(org);
        var rafiq = await api.CreateEmployeeAsync(org, manager: nusrat);
        await api.CreateEmployeeAsync(org);
        using var manager = await api.SignInAsync(SystemRoles.Manager, nusrat);

        var page = await TeamCalendarAsync(manager, org);
        var tooLong = await manager.GetAsync($"/api/rosters/calendar?fromDate={From:yyyy-MM-dd}&toDate={From.AddDays(92):yyyy-MM-dd}");

        page.Items.Select(r => r.Employee.PublicId).Should().BeEquivalentTo([nusrat.PublicId, rafiq.PublicId]);
        (await tooLong.ReadValidationErrorKeysAsync()).Should().Equal("toDate");
    }

    [Fact]
    public async Task Assigning_a_selection_rosters_only_those_employees_and_dates_and_skips_dates_before_joining()
    {
        var org = await api.CreateOrgAsync();
        var rafiq = await api.CreateEmployeeAsync(org);
        var joiner = await api.CreateEmployeeAsync(org, joiningDate: From.AddDays(5));
        var colleague = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var day = await hrManager.CreateShiftAsync();
        var night = await hrManager.CreateShiftAsync("22:00:00", "06:00:00");
        var existing = await (await hrManager.PostAsync("/api/rosters", new { employeeId = rafiq.PublicId, rosterDate = From, shiftId = day.PublicId }))
            .ReadAsync<RosterEntryDto>(HttpStatusCode.Created);

        var result = await (await hrManager.PostAsync("/api/rosters/assign", new
        {
            employeeIds = new[] { rafiq.PublicId, joiner.PublicId },
            fromDate = From,
            toDate = To,
            shiftId = night.PublicId,
            isWeeklyOffOverride = false,
            remarks = "Night cover",
        })).ReadAsync<BulkRosterResult>();

        result.Should().Be(new BulkRosterResult(EmployeeCount: 2, DayCount: 7, Created: 6 + 2, Updated: 1));
        var rows = await RowsAsync(rafiq, joiner, colleague);
        rows.Where(r => r.EmployeeId == rafiq.Id).Should().HaveCount(7);
        rows.Where(r => r.EmployeeId == joiner.Id).Select(r => r.RosterDate).Should().BeEquivalentTo([From.AddDays(5), From.AddDays(6)]);
        rows.Should().NotContain(r => r.EmployeeId == colleague.Id, "the colleague wasn't selected");
        rows.Should().OnlyContain(r => r.Shift.PublicId == night.PublicId && r.IsWeeklyOffOverride == false && r.Remarks == "Night cover");
        rows.Single(r => r.EmployeeId == rafiq.Id && r.RosterDate == From).PublicId.Should().Be(existing.PublicId);

        var request = api.Recalculations.For(joiner).Should().ContainSingle().Subject;
        request.EmployeeIds.Should().BeEquivalentTo([rafiq.Id, joiner.Id]);
        (request.From, request.To).Should().Be((From, To));
    }

    [Fact]
    public async Task Clearing_a_selection_removes_its_entries_so_the_dates_fall_back_to_the_defaults()
    {
        var org = await api.CreateOrgAsync();
        var rafiq = await api.CreateEmployeeAsync(org);
        var colleague = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var general = await hrManager.CreateShiftAsync();
        var night = await hrManager.CreateShiftAsync("22:00:00", "06:00:00");
        await hrManager.SetScheduleAsync(rafiq, general.PublicId);
        await AssignAsync(hrManager, [rafiq, colleague], night, From, To);
        var requestsBefore = api.Recalculations.For(rafiq).Count;

        var result = await (await hrManager.PostAsync("/api/rosters/clear", new
        {
            employeeIds = new[] { rafiq.PublicId }, fromDate = From.AddDays(2), toDate = To.AddDays(10),
        })).ReadAsync<ClearRosterResult>();
        var again = await (await hrManager.PostAsync("/api/rosters/clear", new
        {
            employeeIds = new[] { rafiq.PublicId }, fromDate = From.AddDays(2), toDate = To,
        })).ReadAsync<ClearRosterResult>();

        result.Should().Be(new ClearRosterResult(EmployeeCount: 1, DayCount: 15, Removed: 5));
        again.Removed.Should().Be(0);
        var rows = await RowsAsync(rafiq, colleague);
        rows.Where(r => r.EmployeeId == rafiq.Id && !r.IsDeleted).Select(r => r.RosterDate).Should().BeEquivalentTo([From, From.AddDays(1)]);
        rows.Where(r => r.EmployeeId == rafiq.Id && r.IsDeleted).Should().OnlyContain(r => r.UpdatedBy == hrManager.User.PublicId.ToString());
        rows.Where(r => r.EmployeeId == colleague.Id).Should().HaveCount(7).And.OnlyContain(r => !r.IsDeleted);

        var calendar = (await TeamCalendarAsync(hrManager, org)).Items.Single(r => r.Employee.PublicId == rafiq.PublicId);
        calendar.Days[2].Should().Be(new CalendarDayDto(From.AddDays(2), DayType.Working, calendar.DefaultShift, false, null));

        var requests = api.Recalculations.For(rafiq);
        requests.Should().HaveCount(requestsBefore + 1, "the second clear removed nothing");
        (requests[^1].From, requests[^1].To).Should().Be((From.AddDays(2), To));
    }

    [Fact]
    public async Task A_selection_with_anyone_outside_the_scope_or_inactive_is_refused_whole()
    {
        var org = await api.CreateOrgAsync();
        var nusrat = await api.CreateEmployeeAsync(org);
        var rafiq = await api.CreateEmployeeAsync(org, manager: nusrat);
        var outsider = await api.CreateEmployeeAsync(org);
        var leaver = await api.CreateEmployeeAsync(org, manager: nusrat, isActive: false);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        using var manager = await api.SignInAsync(SystemRoles.Manager, nusrat);
        var shift = await hrManager.CreateShiftAsync();

        foreach (var other in new[] { outsider, leaver })
        {
            var assign = await manager.PostAsync("/api/rosters/assign", new
            {
                employeeIds = new[] { rafiq.PublicId, other.PublicId }, fromDate = From, toDate = To, shiftId = shift.PublicId,
            });
            var clear = await manager.PostAsync("/api/rosters/clear", new
            {
                employeeIds = new[] { rafiq.PublicId, other.PublicId }, fromDate = From, toDate = To,
            });

            (await assign.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("roster.employee_not_found");
            (await clear.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("roster.employee_not_found");
        }

        (await RowsAsync(rafiq, outsider, leaver)).Should().BeEmpty("a refused selection saves nothing");
        (await AssignAsync(manager, [nusrat, rafiq], shift, From, To)).EmployeeCount.Should().Be(2);
    }

    [Fact]
    public async Task Invalid_selections_are_refused()
    {
        var org = await api.CreateOrgAsync();
        var rafiq = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        using var employee = await api.SignInAsync(SystemRoles.Employee, rafiq);
        var shift = await hrManager.CreateShiftAsync();

        var empty = await hrManager.PostAsync("/api/rosters/assign", new
        {
            employeeIds = Array.Empty<Guid>(), fromDate = From, toDate = To, shiftId = shift.PublicId,
        });
        var duplicate = await hrManager.PostAsync("/api/rosters/clear", new
        {
            employeeIds = new[] { rafiq.PublicId, rafiq.PublicId }, fromDate = From, toDate = To,
        });
        var tooMany = await hrManager.PostAsync("/api/rosters/assign", new
        {
            employeeIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()), fromDate = From, toDate = To, shiftId = shift.PublicId,
        });
        var backwards = await hrManager.PostAsync("/api/rosters/clear", new
        {
            employeeIds = new[] { rafiq.PublicId }, fromDate = To, toDate = From,
        });
        var forbidden = await employee.PostAsync("/api/rosters/assign", new
        {
            employeeIds = new[] { rafiq.PublicId }, fromDate = From, toDate = To, shiftId = shift.PublicId,
        });

        (await empty.ReadValidationErrorKeysAsync()).Should().Equal("employeeIds");
        (await duplicate.ReadValidationErrorKeysAsync()).Should().Equal("employeeIds");
        (await tooMany.ReadValidationErrorKeysAsync()).Should().Equal("employeeIds");
        (await backwards.ReadValidationErrorKeysAsync()).Should().Equal("toDate");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<PagedResult<TeamCalendarRowDto>> TeamCalendarAsync(ApiSession session, TestOrg org) =>
        await (await session.GetAsync(
                $"/api/rosters/calendar?fromDate={From:yyyy-MM-dd}&toDate={To:yyyy-MM-dd}&departmentId={org.Department.PublicId}"))
            .ReadAsync<PagedResult<TeamCalendarRowDto>>();

    private static async Task<BulkRosterResult> AssignAsync(
        ApiSession session, TestEmployee[] employees, ShiftDto shift, DateOnly from, DateOnly to) =>
        await (await session.PostAsync("/api/rosters/assign", new
        {
            employeeIds = employees.Select(e => e.PublicId), fromDate = from, toDate = to, shiftId = shift.PublicId,
        })).ReadAsync<BulkRosterResult>();

    private Task<List<EmployeeShiftRoster>> RowsAsync(params TestEmployee[] employees)
    {
        var ids = employees.Select(e => e.Id).ToArray();
        return api.QueryDbAsync(db => db.EmployeeShiftRosters.IgnoreQueryFilters()
            .Include(r => r.Shift)
            .Where(r => ids.Contains(r.EmployeeId))
            .ToListAsync(Ct));
    }
}
