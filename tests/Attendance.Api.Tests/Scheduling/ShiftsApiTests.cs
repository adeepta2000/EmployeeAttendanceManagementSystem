using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Application.Scheduling;
using Attendance.Infrastructure.Persistence.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Tests.Scheduling;

/// <summary>Shift definitions (FR-2.1).</summary>
[Collection(ApiCollection.Name)]
public sealed class ShiftsApiTests(AttendanceApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HR_manager_defines_a_night_shift_and_crossing_midnight_is_derived_from_the_times()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var code = AttendanceApiFactory.UniqueCode("n").ToLowerInvariant();

        // Times may be sent without seconds; they come back with them.
        var response = await hrManager.PostAsync("/api/shifts", SchedulingApi.ShiftBody($" {code} ", "22:00", "06:00"));

        var created = await response.ReadAsync<ShiftDto>(HttpStatusCode.Created);
        created.Code.Should().Be(code.ToUpperInvariant());
        created.StartTime.Should().Be(new TimeOnly(22, 0));
        created.EndTime.Should().Be(new TimeOnly(6, 0));
        created.CrossesMidnight.Should().BeTrue();
        created.DurationMinutes.Should().Be(480);
        created.IsActive.Should().BeTrue();
        response.Headers.Location!.AbsolutePath.Should().Be($"/api/shifts/{created.PublicId}");
    }

    [Theory]
    [InlineData("22:00:00", "06:00:00", false)]
    [InlineData("09:00:00", "18:00:00", true)]
    public async Task A_crossesMidnight_flag_that_contradicts_the_times_is_refused(string start, string end, bool crossesMidnight)
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);

        var response = await hrManager.PostAsync(
            "/api/shifts", SchedulingApi.ShiftBody(AttendanceApiFactory.UniqueCode("S"), start, end, crossesMidnight));

        (await response.ReadValidationErrorKeysAsync()).Should().Equal("crossesMidnight");
    }

    [Fact]
    public async Task A_crossesMidnight_flag_that_agrees_with_the_times_is_accepted()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);

        var response = await hrManager.PostAsync(
            "/api/shifts", SchedulingApi.ShiftBody(AttendanceApiFactory.UniqueCode("S"), "22:00:00", "06:00:00", crossesMidnight: true));

        (await response.ReadAsync<ShiftDto>(HttpStatusCode.Created)).CrossesMidnight.Should().BeTrue();
    }

    [Fact]
    public async Task Inconsistent_shift_rules_are_refused()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var code = AttendanceApiFactory.UniqueCode("S");

        var sameTimes = await hrManager.PostAsync("/api/shifts", SchedulingApi.ShiftBody(code, "09:00:00", "09:00:00"));
        var halfDayAboveFullDay = await hrManager.PostAsync("/api/shifts", new
        {
            code, name = "X", startTime = "09:00:00", endTime = "18:00:00", breakMinutes = 60,
            halfDayThresholdMinutes = 500, fullDayMinutes = 480,
        });
        var fullDayLongerThanShift = await hrManager.PostAsync("/api/shifts", new
        {
            code, name = "X", startTime = "09:00:00", endTime = "13:00:00", breakMinutes = 30,
            halfDayThresholdMinutes = 120, fullDayMinutes = 240,
        });
        var windowOver24Hours = await hrManager.PostAsync("/api/shifts", new
        {
            code, name = "X", startTime = "08:00:00", endTime = "20:00:00", breakMinutes = 60,
            halfDayThresholdMinutes = 240, fullDayMinutes = 480, punchWindowBeforeMinutes = 360, punchWindowAfterMinutes = 361,
        });
        var noTimes = await hrManager.PostAsync("/api/shifts", new { code, name = "X", halfDayThresholdMinutes = 240, fullDayMinutes = 480 });

        (await sameTimes.ReadValidationErrorKeysAsync()).Should().Contain("endTime");
        (await halfDayAboveFullDay.ReadValidationErrorKeysAsync()).Should().Equal("halfDayThresholdMinutes");
        (await fullDayLongerThanShift.ReadValidationErrorKeysAsync()).Should().Equal("fullDayMinutes");
        (await windowOver24Hours.ReadValidationErrorKeysAsync()).Should().Equal("punchWindowAfterMinutes");
        (await noTimes.ReadValidationErrorKeysAsync()).Should().Contain(["startTime", "endTime"]);
    }

    [Fact]
    public async Task Changing_the_times_derives_crossing_midnight_again()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var shift = await hrManager.CreateShiftAsync("14:00:00", "22:00:00");

        var updated = await (await hrManager.PutAsync($"/api/shifts/{shift.PublicId}", SchedulingApi.ShiftBody(shift.Code, "22:00:00", "06:00:00")))
            .ReadAsync<ShiftDto>();

        shift.CrossesMidnight.Should().BeFalse();
        updated.CrossesMidnight.Should().BeTrue();
        (await api.QueryDbAsync(db => db.Shifts.SingleAsync(s => s.PublicId == shift.PublicId, Ct))).CrossesMidnight.Should().BeTrue();
    }

    [Fact]
    public async Task Any_signed_in_user_can_read_shifts_but_only_shift_write_can_change_them()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        using var hrOfficer = await api.SignInAsync(SystemRoles.HROfficer);
        using var employee = await api.SignInAsync(SystemRoles.Employee);
        var shift = await hrManager.CreateShiftAsync();

        (await employee.GetAsync("/api/shifts?isActive=true")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await employee.GetAsync($"/api/shifts/{shift.PublicId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await hrOfficer.PostAsync("/api/shifts", SchedulingApi.ShiftBody("X1", "09:00:00", "18:00:00")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await employee.PutAsync($"/api/shifts/{shift.PublicId}", SchedulingApi.ShiftBody(shift.Code, "09:00:00", "18:00:00")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_shift_code_can_be_used_only_once()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var shift = await hrManager.CreateShiftAsync();

        var response = await hrManager.PostAsync("/api/shifts", SchedulingApi.ShiftBody(shift.Code.ToLowerInvariant(), "08:00:00", "17:00:00"));

        (await response.ReadProblemCodeAsync(HttpStatusCode.Conflict)).Should().Be("shift.code_taken");
    }

    [Fact]
    public async Task A_shift_that_is_an_active_employees_default_cannot_be_retired()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var shift = await hrManager.CreateShiftAsync();
        await hrManager.SetScheduleAsync(employee, shift.PublicId);

        var refused = await hrManager.PostAsync($"/api/shifts/{shift.PublicId}/deactivate");
        await hrManager.SetScheduleAsync(employee, defaultShiftId: null);
        var retired = await (await hrManager.PostAsync($"/api/shifts/{shift.PublicId}/deactivate")).ReadAsync<ShiftDto>();

        (await refused.ReadProblemCodeAsync(HttpStatusCode.Conflict)).Should().Be("shift.in_use");
        retired.IsActive.Should().BeFalse();
        (await (await hrManager.GetAsync("/api/shifts?isActive=true")).ReadAsync<List<ShiftDto>>())
            .Should().NotContain(s => s.PublicId == shift.PublicId);
    }
}
