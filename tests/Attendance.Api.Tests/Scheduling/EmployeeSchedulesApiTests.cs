using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Application.Scheduling;
using Attendance.Domain.Scheduling;
using Attendance.Infrastructure.Persistence.Seeding;
using FluentAssertions;

namespace Attendance.Api.Tests.Scheduling;

/// <summary>
/// An employee's default shift and weekly-off pattern (FR-2.2, FR-2.3) and the calendar the
/// <see cref="CalendarService"/> resolves from what is stored. Calendar dates are in 2032, a year no other
/// test class adds holidays to.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class EmployeeSchedulesApiTests(AttendanceApiFactory api)
{
    [Fact]
    public async Task HR_assigns_a_default_shift_and_weekly_off_pattern_and_can_clear_them()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        using var hrOfficer = await api.SignInAsync(SystemRoles.HROfficer);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var shift = await hrManager.CreateShiftAsync();
        var pattern = await hrManager.CreateFridayAndAlternateSaturdaysAsync();

        var assigned = await hrOfficer.SetScheduleAsync(employee, shift.PublicId, pattern.PublicId);
        var read = await (await hrOfficer.GetAsync($"/api/employees/{employee.PublicId}/schedule")).ReadAsync<EmployeeScheduleDto>();
        var cleared = await hrOfficer.SetScheduleAsync(employee, defaultShiftId: null, weeklyOffPatternId: null);

        assigned.DefaultShift.Should().Be(new ShiftRef(shift.PublicId, shift.Code, shift.Name));
        assigned.WeeklyOffPattern.Should().Be(new WeeklyOffPatternRef(pattern.PublicId, pattern.Name));
        read.Should().BeEquivalentTo(assigned);
        cleared.DefaultShift.Should().BeNull();
        cleared.WeeklyOffPattern.Should().BeNull();
    }

    [Fact]
    public async Task A_default_shift_or_pattern_must_exist_and_be_active()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var shift = await hrManager.CreateShiftAsync();
        var retired = await hrManager.CreateShiftAsync();
        await hrManager.PostAsync($"/api/shifts/{retired.PublicId}/deactivate");

        var toRetired = await hrManager.PutAsync($"/api/employees/{employee.PublicId}/schedule", new { defaultShiftId = retired.PublicId });
        var unknownPattern = await hrManager.PutAsync($"/api/employees/{employee.PublicId}/schedule", new
        {
            defaultShiftId = shift.PublicId, weeklyOffPatternId = Guid.NewGuid(),
        });

        (await toRetired.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("employee.shift_not_found");
        (await unknownPattern.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("employee.weekly_off_pattern_not_found");
        (await (await hrManager.GetAsync($"/api/employees/{employee.PublicId}/schedule")).ReadAsync<EmployeeScheduleDto>())
            .DefaultShift.Should().BeNull("a refused change saves nothing");
    }

    [Fact]
    public async Task The_calendar_resolves_rosters_weekly_offs_and_local_holidays_from_the_database()
    {
        var dhaka = await api.CreateOrgAsync();
        var chittagong = await api.CreateOrgAsync();
        var rafiq = await api.CreateEmployeeAsync(dhaka);
        var nusrat = await api.CreateEmployeeAsync(chittagong);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var general = await hrManager.CreateShiftAsync();
        var night = await hrManager.CreateShiftAsync("22:00:00", "06:00:00");
        var pattern = await hrManager.CreateFridayAndAlternateSaturdaysAsync();
        await hrManager.SetScheduleAsync(rafiq, general.PublicId, pattern.PublicId);
        await hrManager.SetScheduleAsync(nusrat, general.PublicId, pattern.PublicId);

        // May 2032 starts on a Saturday: Fridays 7, 14, 21, 28; 2nd and 4th Saturdays 8 and 22.
        var holidayName = $"Dhaka City Day {Guid.NewGuid():N}";
        (await hrManager.PostAsync("/api/holidays", new
        {
            name = holidayName, holidayDate = "2032-05-04", workLocationIds = new[] { dhaka.WorkLocation.PublicId },
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await hrManager.PostAsync("/api/rosters", new
        {
            employeeId = rafiq.PublicId, rosterDate = "2032-05-10", shiftId = night.PublicId,
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await hrManager.PostAsync("/api/rosters", new
        {
            employeeId = rafiq.PublicId, rosterDate = "2032-05-08", shiftId = general.PublicId, isWeeklyOffOverride = false,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var rafiqsMay = await CalendarAsync(hrManager, rafiq, "2032-05-01", "2032-05-31");
        var nusratsMay = await CalendarAsync(hrManager, nusrat, "2032-05-01", "2032-05-31");

        rafiqsMay.Should().HaveCount(31);
        rafiqsMay.Where(d => d.DayType == DayType.WeeklyOff).Select(d => d.Date.Day)
            .Should().Equal([7, 14, 21, 22, 28], "the 8th is a swapped working day");
        rafiqsMay.Single(d => d.Date.Day == 4).Should().Be(
            new CalendarDayDto(new DateOnly(2032, 5, 4), DayType.Holiday, ShiftRefOf(general), IsRostered: false, holidayName));
        rafiqsMay.Single(d => d.Date.Day == 10).Should().Be(
            new CalendarDayDto(new DateOnly(2032, 5, 10), DayType.Working, ShiftRefOf(night), IsRostered: true, HolidayName: null));
        rafiqsMay.Single(d => d.Date.Day == 8).Should().Be(
            new CalendarDayDto(new DateOnly(2032, 5, 8), DayType.Working, ShiftRefOf(general), IsRostered: true, HolidayName: null));

        nusratsMay.Single(d => d.Date.Day == 4).DayType.Should().Be(DayType.Working, "the holiday is limited to Dhaka");
        nusratsMay.Where(d => d.DayType == DayType.WeeklyOff).Select(d => d.Date.Day).Should().Equal([7, 8, 14, 21, 22, 28]);
    }

    [Fact]
    public async Task A_manager_gets_404_for_the_schedule_and_calendar_of_anyone_outside_their_tree()
    {
        var org = await api.CreateOrgAsync();
        var nusrat = await api.CreateEmployeeAsync(org);
        var rafiq = await api.CreateEmployeeAsync(org, manager: nusrat);
        var outsider = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        using var manager = await api.SignInAsync(SystemRoles.Manager, nusrat);
        var shift = await hrManager.CreateShiftAsync();

        (await manager.SetScheduleAsync(rafiq, shift.PublicId)).DefaultShift!.PublicId.Should().Be(shift.PublicId);
        foreach (var response in new[]
        {
            await manager.GetAsync($"/api/employees/{outsider.PublicId}/schedule"),
            await manager.PutAsync($"/api/employees/{outsider.PublicId}/schedule", new { defaultShiftId = shift.PublicId }),
            await manager.GetAsync($"/api/employees/{outsider.PublicId}/calendar?fromDate=2032-01-01&toDate=2032-01-31"),
        })
        {
            (await response.ReadProblemCodeAsync(HttpStatusCode.NotFound)).Should().Be("employee.not_found");
        }

        var outsidersSchedule = await (await hrManager.GetAsync($"/api/employees/{outsider.PublicId}/schedule")).ReadAsync<EmployeeScheduleDto>();
        outsidersSchedule.DefaultShift.Should().BeNull();
    }

    [Fact]
    public async Task An_employee_reads_their_own_calendar_but_cannot_change_their_schedule()
    {
        var org = await api.CreateOrgAsync();
        var rafiq = await api.CreateEmployeeAsync(org);
        using var employee = await api.SignInAsync(SystemRoles.Employee, rafiq);

        var calendar = await CalendarAsync(employee, rafiq, "2032-01-01", "2032-01-07");
        var change = await employee.PutAsync($"/api/employees/{rafiq.PublicId}/schedule", new { defaultShiftId = (Guid?)null });
        var tooLong = await employee.GetAsync($"/api/employees/{rafiq.PublicId}/calendar?fromDate=2032-01-01&toDate=2032-04-02");

        calendar.Should().HaveCount(7).And.OnlyContain(d => d.DayType == DayType.Working && d.Shift == null);
        change.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tooLong.ReadValidationErrorKeysAsync()).Should().Equal("toDate");
    }

    private static ShiftRef ShiftRefOf(ShiftDto shift) => new(shift.PublicId, shift.Code, shift.Name);

    private static async Task<List<CalendarDayDto>> CalendarAsync(ApiSession session, TestEmployee employee, string from, string to) =>
        await (await session.GetAsync($"/api/employees/{employee.PublicId}/calendar?fromDate={from}&toDate={to}"))
            .ReadAsync<List<CalendarDayDto>>();
}
