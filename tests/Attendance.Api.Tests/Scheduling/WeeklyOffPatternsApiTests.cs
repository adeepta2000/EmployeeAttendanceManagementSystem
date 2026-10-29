using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Application.Scheduling;
using Attendance.Infrastructure.Persistence.Seeding;
using FluentAssertions;

namespace Attendance.Api.Tests.Scheduling;

/// <summary>Weekly-off patterns (FR-2.2).</summary>
[Collection(ApiCollection.Name)]
public sealed class WeeklyOffPatternsApiTests(AttendanceApiFactory api)
{
    [Fact]
    public async Task HR_manager_defines_every_Friday_and_Friday_plus_alternate_Saturdays()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);

        var fridays = await (await hrManager.PostAsync("/api/weekly-off-patterns", new
        {
            name = $"Friday {Guid.NewGuid():N}",
            days = new[] { new { dayOfWeek = "Friday" } },
        })).ReadAsync<WeeklyOffPatternDto>(HttpStatusCode.Created);
        var alternateSaturdays = await hrManager.CreateFridayAndAlternateSaturdaysAsync();

        fridays.Days.Should().Equal(new WeeklyOffDayDto(DayOfWeek.Friday, null));
        alternateSaturdays.Days.Should().Equal(
            new WeeklyOffDayDto(DayOfWeek.Friday, null),
            new WeeklyOffDayDto(DayOfWeek.Saturday, 2),
            new WeeklyOffDayDto(DayOfWeek.Saturday, 4));
        (await (await hrManager.GetAsync($"/api/weekly-off-patterns/{alternateSaturdays.PublicId}")).ReadAsync<WeeklyOffPatternDto>())
            .Should().BeEquivalentTo(alternateSaturdays);
    }

    [Fact]
    public async Task Patterns_that_make_no_sense_are_refused()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);

        async Task<IReadOnlyCollection<string>> CreateAsync(params object[] days) =>
            await (await hrManager.PostAsync("/api/weekly-off-patterns", new { name = $"P {Guid.NewGuid():N}", days }))
                .ReadValidationErrorKeysAsync();

        (await CreateAsync()).Should().Contain("days");
        (await CreateAsync(new { dayOfWeek = "Friday" }, new { dayOfWeek = "Friday" })).Should().Contain("days");
        (await CreateAsync(new { dayOfWeek = "Friday" }, new { dayOfWeek = "Friday", weekOfMonth = 2 })).Should().Contain("days");
        (await CreateAsync(new { dayOfWeek = "Saturday", weekOfMonth = 6 })).Should().Contain(key => key.StartsWith("days[0]"));
    }

    [Fact]
    public async Task Renaming_keeps_the_days_and_a_name_can_be_used_only_once()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var first = await hrManager.CreateFridayAndAlternateSaturdaysAsync();
        var second = await hrManager.CreateFridayAndAlternateSaturdaysAsync();
        var newName = $"Renamed {Guid.NewGuid():N}";

        var renamed = await (await hrManager.PutAsync($"/api/weekly-off-patterns/{first.PublicId}", new { name = newName }))
            .ReadAsync<WeeklyOffPatternDto>();
        var taken = await hrManager.PutAsync($"/api/weekly-off-patterns/{second.PublicId}", new { name = newName.ToUpperInvariant() });

        renamed.Should().BeEquivalentTo(first with { Name = newName });
        (await taken.ReadProblemCodeAsync(HttpStatusCode.Conflict)).Should().Be("weekly_off_pattern.name_taken");
    }

    [Fact]
    public async Task A_pattern_active_employees_follow_cannot_be_retired_and_only_shift_write_can_change_patterns()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        using var manager = await api.SignInAsync(SystemRoles.Manager);
        var pattern = await hrManager.CreateFridayAndAlternateSaturdaysAsync();
        await hrManager.SetScheduleAsync(employee, defaultShiftId: null, pattern.PublicId);

        var refused = await hrManager.PostAsync($"/api/weekly-off-patterns/{pattern.PublicId}/deactivate");
        var forbidden = await manager.PostAsync($"/api/weekly-off-patterns/{pattern.PublicId}/deactivate");

        (await refused.ReadProblemCodeAsync(HttpStatusCode.Conflict)).Should().Be("weekly_off_pattern.in_use");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.GetAsync("/api/weekly-off-patterns")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
