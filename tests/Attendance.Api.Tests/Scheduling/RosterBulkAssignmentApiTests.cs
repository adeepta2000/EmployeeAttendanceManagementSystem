using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Application.Scheduling;
using Attendance.Infrastructure.Persistence.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Tests.Scheduling;

/// <summary>Rostering a whole department over a date range (FR-2.4), written set-based.</summary>
[Collection(ApiCollection.Name)]
public sealed class RosterBulkAssignmentApiTests(AttendanceApiFactory api)
{
    private static readonly DateOnly From = new(2027, 6, 1);
    private static readonly DateOnly To = new(2027, 6, 30);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_active_employee_of_the_department_gets_one_entry_per_date_from_their_joining_date()
    {
        var org = await api.CreateOrgAsync();
        var veteran = await api.CreateEmployeeAsync(org);
        var joiner = await api.CreateEmployeeAsync(org, joiningDate: new DateOnly(2027, 6, 21));
        var leaver = await api.CreateEmployeeAsync(org, isActive: false);
        var elsewhere = await api.CreateEmployeeAsync(await api.CreateOrgAsync());
        using var hrOfficer = await api.SignInAsync(SystemRoles.HROfficer);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var night = await hrManager.CreateShiftAsync("22:00:00", "06:00:00");

        var result = await (await hrOfficer.PostAsync("/api/rosters/bulk", new
        {
            departmentId = org.Department.PublicId, fromDate = From, toDate = To, shiftId = night.PublicId, remarks = "June nights",
        })).ReadAsync<BulkRosterResult>();

        result.Should().Be(new BulkRosterResult(EmployeeCount: 2, DayCount: 30, Created: 30 + 10, Updated: 0));

        var rows = await RowsAsync(veteran, joiner, leaver, elsewhere);
        rows.Where(r => r.EmployeeId == veteran.Id).Select(r => r.RosterDate).Should().BeEquivalentTo(DatesFrom(From, To));
        rows.Where(r => r.EmployeeId == joiner.Id).Select(r => r.RosterDate).Should().BeEquivalentTo(DatesFrom(new DateOnly(2027, 6, 21), To));
        rows.Should().NotContain(r => r.EmployeeId == leaver.Id || r.EmployeeId == elsewhere.Id);
        rows.Should().OnlyContain(r => r.Shift.PublicId == night.PublicId && r.Remarks == "June nights" && r.IsWeeklyOffOverride == null);
        rows.Should().OnlyContain(r => r.CreatedBy == hrOfficer.User.PublicId.ToString() && r.PublicId != Guid.Empty);
        rows.Select(r => r.PublicId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Existing_entries_in_the_range_are_overwritten_removed_ones_restored_and_matching_ones_left_alone()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var day = await hrManager.CreateShiftAsync();
        var night = await hrManager.CreateShiftAsync("22:00:00", "06:00:00");
        var differing = await CreateEntryAsync(hrManager, employee, day, From);
        var removed = await CreateEntryAsync(hrManager, employee, night, From.AddDays(1));
        await hrManager.DeleteAsync($"/api/rosters/{removed.PublicId}");
        var matching = await CreateEntryAsync(hrManager, employee, night, From.AddDays(2));

        var result = await BulkAsync(hrManager, org, night, From, From.AddDays(4));

        // 2 updated (the differing and the removed entry), 1 left as it was, 2 created.
        result.Should().Be(new BulkRosterResult(EmployeeCount: 1, DayCount: 5, Created: 2, Updated: 2));
        var rows = await RowsAsync(employee);
        rows.Should().HaveCount(5).And.OnlyContain(r => r.Shift.PublicId == night.PublicId && !r.IsDeleted);
        rows.Single(r => r.RosterDate == From).PublicId.Should().Be(differing.PublicId, "the existing row was updated in place");
        rows.Single(r => r.RosterDate == From.AddDays(2)).UpdatedAt.Should().BeNull("an entry that already matched isn't rewritten");
    }

    [Fact]
    public async Task Running_the_same_assignment_twice_changes_nothing_the_second_time()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var shift = await hrManager.CreateShiftAsync();

        await BulkAsync(hrManager, org, shift, From, To);
        var requestsAfterFirst = api.Recalculations.For(employee).Count;
        var second = await BulkAsync(hrManager, org, shift, From, To);

        second.Should().Be(new BulkRosterResult(EmployeeCount: 1, DayCount: 30, Created: 0, Updated: 0));
        api.Recalculations.For(employee).Should().HaveCount(requestsAfterFirst, "nothing changed, so nothing needs recalculating");
        (await RowsAsync(employee)).Should().HaveCount(30);
    }

    [Fact]
    public async Task One_recalculation_request_covers_every_employee_and_date_that_changed()
    {
        var org = await api.CreateOrgAsync();
        var first = await api.CreateEmployeeAsync(org);
        var second = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var shift = await hrManager.CreateShiftAsync();

        await BulkAsync(hrManager, org, shift, From, To);

        var request = api.Recalculations.For(first).Should().ContainSingle().Subject;
        request.EmployeeIds.Should().BeEquivalentTo([first.Id, second.Id]);
        (request.From, request.To).Should().Be((From, To));
        api.Recalculations.For(second).Should().ContainSingle().Which.Should().BeSameAs(request);
    }

    [Fact]
    public async Task A_manager_rosters_only_the_part_of_the_department_in_their_reporting_tree()
    {
        var org = await api.CreateOrgAsync();
        var nusrat = await api.CreateEmployeeAsync(org);
        var rafiq = await api.CreateEmployeeAsync(org, manager: nusrat);
        var outsider = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        using var manager = await api.SignInAsync(SystemRoles.Manager, nusrat);
        var shift = await hrManager.CreateShiftAsync();

        var result = await BulkAsync(manager, org, shift, From, From.AddDays(6));

        result.EmployeeCount.Should().Be(2, "Nusrat and Rafiq; the outsider isn't in her tree");
        (await RowsAsync(nusrat, rafiq)).Should().HaveCount(14);
        (await RowsAsync(outsider)).Should().BeEmpty();
    }

    [Fact]
    public async Task Invalid_assignments_are_refused()
    {
        var org = await api.CreateOrgAsync();
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        using var employee = await api.SignInAsync(SystemRoles.Employee);
        var shift = await hrManager.CreateShiftAsync();

        var tooLong = await hrManager.PostAsync("/api/rosters/bulk", new
        {
            departmentId = org.Department.PublicId, fromDate = From, toDate = From.AddDays(92), shiftId = shift.PublicId,
        });
        var backwards = await hrManager.PostAsync("/api/rosters/bulk", new
        {
            departmentId = org.Department.PublicId, fromDate = To, toDate = From, shiftId = shift.PublicId,
        });
        var unknownDepartment = await hrManager.PostAsync("/api/rosters/bulk", new
        {
            departmentId = Guid.NewGuid(), fromDate = From, toDate = To, shiftId = shift.PublicId,
        });
        var forbidden = await employee.PostAsync("/api/rosters/bulk", new
        {
            departmentId = org.Department.PublicId, fromDate = From, toDate = To, shiftId = shift.PublicId,
        });

        (await tooLong.ReadValidationErrorKeysAsync()).Should().Equal("toDate");
        (await backwards.ReadValidationErrorKeysAsync()).Should().Equal("toDate");
        (await unknownDepartment.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("roster.department_not_found");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<BulkRosterResult> BulkAsync(ApiSession session, TestOrg org, ShiftDto shift, DateOnly from, DateOnly to) =>
        await (await session.PostAsync("/api/rosters/bulk", new
        {
            departmentId = org.Department.PublicId, fromDate = from, toDate = to, shiftId = shift.PublicId,
        })).ReadAsync<BulkRosterResult>();

    private static async Task<RosterEntryDto> CreateEntryAsync(ApiSession session, TestEmployee employee, ShiftDto shift, DateOnly date) =>
        await (await session.PostAsync("/api/rosters", new { employeeId = employee.PublicId, rosterDate = date, shiftId = shift.PublicId }))
            .ReadAsync<RosterEntryDto>(HttpStatusCode.Created);

    private static IEnumerable<DateOnly> DatesFrom(DateOnly from, DateOnly to) =>
        Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(from.AddDays);

    private Task<List<Domain.Scheduling.EmployeeShiftRoster>> RowsAsync(params TestEmployee[] employees)
    {
        var ids = employees.Select(e => e.Id).ToArray();
        return api.QueryDbAsync(db => db.EmployeeShiftRosters.IgnoreQueryFilters()
            .Include(r => r.Shift)
            .Where(r => ids.Contains(r.EmployeeId))
            .ToListAsync(Ct));
    }
}
