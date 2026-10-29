using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Application.Organization;
using Attendance.Application.Scheduling;
using Attendance.Domain.Processing;
using Attendance.Infrastructure.Persistence.Seeding;
using Attendance.Shared.Paging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Tests.Scheduling;

/// <summary>
/// Roster entries (FR-2.3), their recalculation requests (FR-2.5), and data scoping with IDOR defence on
/// every <c>{publicId}</c> route (FR-8.3): an entry of someone outside the caller's scope is a 404.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RostersApiTests(AttendanceApiFactory api)
{
    private static readonly DateOnly Date = new(2027, 3, 1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HR_rosters_a_shift_for_a_date_and_the_date_is_recalculated()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org, firstName: "Rafiq", lastName: "Islam");
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var night = await hrManager.CreateShiftAsync("22:00:00", "06:00:00");

        var response = await hrManager.PostAsync("/api/rosters", new
        {
            employeeId = employee.PublicId, rosterDate = Date, shiftId = night.PublicId, remarks = " Night cover ",
        });

        var entry = await response.ReadAsync<RosterEntryDto>(HttpStatusCode.Created);
        entry.Should().Be(new RosterEntryDto(
            entry.PublicId,
            new EmployeeRef(employee.PublicId, employee.EmployeeCode, "Rafiq Islam"),
            Date,
            new ShiftRef(night.PublicId, night.Code, night.Name),
            IsWeeklyOffOverride: null,
            Remarks: "Night cover"));
        response.Headers.Location!.AbsolutePath.Should().Be($"/api/rosters/{entry.PublicId}");
        api.Recalculations.For(employee).Should().ContainSingle().Which
            .Should().Match<AttendanceRecalculationRequested>(r => r.From == Date && r.To == Date && r.Reason == RecalculationReason.RosterChanged);
    }

    [Fact]
    public async Task An_employee_has_one_entry_per_date()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var shift = await hrManager.CreateShiftAsync();
        var body = new { employeeId = employee.PublicId, rosterDate = Date, shiftId = shift.PublicId };

        (await hrManager.PostAsync("/api/rosters", body)).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await hrManager.PostAsync("/api/rosters", body);

        (await second.ReadProblemCodeAsync(HttpStatusCode.Conflict)).Should().Be("roster.date_taken");
    }

    [Fact]
    public async Task Changing_the_shift_requests_a_recalculation_but_changing_only_the_remarks_does_not()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var day = await hrManager.CreateShiftAsync();
        var night = await hrManager.CreateShiftAsync("22:00:00", "06:00:00");
        var entry = await CreateEntryAsync(hrManager, employee, day);
        var path = $"/api/rosters/{entry.PublicId}";

        var remarksOnly = await (await hrManager.PutAsync(path, new { shiftId = day.PublicId, remarks = "Swapped with Nusrat" }))
            .ReadAsync<RosterEntryDto>();
        var requestsAfterRemarks = api.Recalculations.For(employee).Count;
        var newShift = await (await hrManager.PutAsync(path, new { shiftId = night.PublicId, isWeeklyOffOverride = false }))
            .ReadAsync<RosterEntryDto>();

        remarksOnly.Remarks.Should().Be("Swapped with Nusrat");
        requestsAfterRemarks.Should().Be(1, "only the create asked so far");
        newShift.Shift.PublicId.Should().Be(night.PublicId);
        newShift.IsWeeklyOffOverride.Should().BeFalse();
        newShift.Remarks.Should().BeNull();
        api.Recalculations.For(employee).Should().HaveCount(2);
    }

    [Fact]
    public async Task A_removed_entry_is_soft_deleted_and_its_date_can_be_rostered_again()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var shift = await hrManager.CreateShiftAsync();
        var entry = await CreateEntryAsync(hrManager, employee, shift);

        var deleted = await hrManager.DeleteAsync($"/api/rosters/{entry.PublicId}");
        var afterDelete = await hrManager.GetAsync($"/api/rosters/{entry.PublicId}");
        var again = await (await hrManager.PostAsync("/api/rosters", new
        {
            employeeId = employee.PublicId, rosterDate = Date, shiftId = shift.PublicId, isWeeklyOffOverride = true,
        })).ReadAsync<RosterEntryDto>(HttpStatusCode.Created);

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await afterDelete.ReadProblemCodeAsync(HttpStatusCode.NotFound)).Should().Be("roster.not_found");
        again.IsWeeklyOffOverride.Should().BeTrue();
        api.Recalculations.For(employee).Should().HaveCount(3, "create, remove and create again each change the plan");

        // One row per employee and date: the removed one came back.
        var rows = await api.QueryDbAsync(db => db.EmployeeShiftRosters.IgnoreQueryFilters()
            .Where(r => r.EmployeeId == employee.Id).ToListAsync(Ct));
        rows.Should().ContainSingle().Which.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task An_entry_needs_an_active_employee_who_had_joined_and_an_active_shift()
    {
        var org = await api.CreateOrgAsync();
        var joiner = await api.CreateEmployeeAsync(org, joiningDate: Date);
        var leaver = await api.CreateEmployeeAsync(org, isActive: false);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var shift = await hrManager.CreateShiftAsync();
        var retired = await hrManager.CreateShiftAsync();
        await hrManager.PostAsync($"/api/shifts/{retired.PublicId}/deactivate");

        var beforeJoining = await hrManager.PostAsync("/api/rosters", new { employeeId = joiner.PublicId, rosterDate = Date.AddDays(-1), shiftId = shift.PublicId });
        var inactive = await hrManager.PostAsync("/api/rosters", new { employeeId = leaver.PublicId, rosterDate = Date, shiftId = shift.PublicId });
        var retiredShift = await hrManager.PostAsync("/api/rosters", new { employeeId = joiner.PublicId, rosterDate = Date, shiftId = retired.PublicId });

        (await beforeJoining.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("roster.before_joining");
        (await inactive.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("roster.employee_not_found");
        (await retiredShift.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("roster.shift_not_found");
    }

    [Fact]
    public async Task A_manager_rosters_their_own_team_and_gets_404_for_anyone_elses_entries()
    {
        var org = await api.CreateOrgAsync();
        var nusrat = await api.CreateEmployeeAsync(org);
        var rafiq = await api.CreateEmployeeAsync(org, manager: nusrat);
        var outsider = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        using var manager = await api.SignInAsync(SystemRoles.Manager, nusrat);
        var shift = await hrManager.CreateShiftAsync();
        var outsidersEntry = await CreateEntryAsync(hrManager, outsider, shift);

        var ownTeam = await manager.PostAsync("/api/rosters", new { employeeId = rafiq.PublicId, rosterDate = Date, shiftId = shift.PublicId });
        var otherTeam = await manager.PostAsync("/api/rosters", new { employeeId = outsider.PublicId, rosterDate = Date.AddDays(1), shiftId = shift.PublicId });
        var path = $"/api/rosters/{outsidersEntry.PublicId}";

        ownTeam.StatusCode.Should().Be(HttpStatusCode.Created);
        (await otherTeam.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("roster.employee_not_found");
        foreach (var response in new[]
        {
            await manager.GetAsync(path),
            await manager.PutAsync(path, new { shiftId = shift.PublicId, remarks = "Hijacked" }),
            await manager.DeleteAsync(path),
        })
        {
            (await response.ReadProblemCodeAsync(HttpStatusCode.NotFound)).Should().Be("roster.not_found");
        }

        var untouched = await (await hrManager.GetAsync(path)).ReadAsync<RosterEntryDto>();
        untouched.Remarks.Should().BeNull();
    }

    [Fact]
    public async Task An_employee_reads_only_their_own_entries_and_cannot_change_them()
    {
        var org = await api.CreateOrgAsync();
        var rafiq = await api.CreateEmployeeAsync(org);
        var colleague = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        using var employee = await api.SignInAsync(SystemRoles.Employee, rafiq);
        var shift = await hrManager.CreateShiftAsync();
        var own = await CreateEntryAsync(hrManager, rafiq, shift);
        var colleagues = await CreateEntryAsync(hrManager, colleague, shift);

        var list = await (await employee.GetAsync($"/api/rosters?fromDate={Date:yyyy-MM-dd}&toDate={Date:yyyy-MM-dd}&departmentId={org.Department.PublicId}"))
            .ReadAsync<PagedResult<RosterEntryDto>>();

        list.Items.Select(e => e.PublicId).Should().Equal(own.PublicId);
        (await employee.GetAsync($"/api/rosters/{own.PublicId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await employee.GetAsync($"/api/rosters/{colleagues.PublicId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await employee.PutAsync($"/api/rosters/{own.PublicId}", new { shiftId = shift.PublicId })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_list_filters_by_employee_and_date_range_and_pages()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        var other = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var shift = await hrManager.CreateShiftAsync();
        for (var day = 0; day < 5; day++)
        {
            await CreateEntryAsync(hrManager, employee, shift, Date.AddDays(day));
        }

        await CreateEntryAsync(hrManager, other, shift);

        var page = await (await hrManager.GetAsync(
                $"/api/rosters?fromDate={Date:yyyy-MM-dd}&toDate={Date.AddDays(3):yyyy-MM-dd}&employeeId={employee.PublicId}&page=2&pageSize=3"))
            .ReadAsync<PagedResult<RosterEntryDto>>();
        var tooLong = await hrManager.GetAsync($"/api/rosters?fromDate={Date:yyyy-MM-dd}&toDate={Date.AddDays(92):yyyy-MM-dd}");

        page.TotalCount.Should().Be(4);
        page.Items.Select(e => e.RosterDate).Should().Equal(Date.AddDays(3));
        (await tooLong.ReadValidationErrorKeysAsync()).Should().Equal("toDate");
    }

    private static async Task<RosterEntryDto> CreateEntryAsync(ApiSession session, TestEmployee employee, ShiftDto shift, DateOnly? date = null) =>
        await (await session.PostAsync("/api/rosters", new { employeeId = employee.PublicId, rosterDate = date ?? Date, shiftId = shift.PublicId }))
            .ReadAsync<RosterEntryDto>(HttpStatusCode.Created);
}
