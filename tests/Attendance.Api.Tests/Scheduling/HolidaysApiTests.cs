using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Application.Scheduling;
using Attendance.Infrastructure.Persistence.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Tests.Scheduling;

/// <summary>
/// The holiday calendar with its optional location filter (FR-7.1). Holidays are company-wide and every
/// test shares one database, so these tests use years no other test class touches (2030 and 2031).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class HolidaysApiTests(AttendanceApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_holiday_limited_to_one_location_is_listed_for_that_location_only()
    {
        var dhaka = await api.CreateOrgAsync();
        var chittagong = await api.CreateOrgAsync();
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var name = $"Dhaka City Day {Guid.NewGuid():N}";

        var local = await (await hrManager.PostAsync("/api/holidays", new
        {
            name, holidayDate = "2030-11-05", isOptional = false, workLocationIds = new[] { dhaka.WorkLocation.PublicId },
        })).ReadAsync<HolidayDto>(HttpStatusCode.Created);
        var everywhere = await (await hrManager.PostAsync("/api/holidays", new
        {
            name = $"Foundation Day {Guid.NewGuid():N}", holidayDate = "2030-11-06",
        })).ReadAsync<HolidayDto>(HttpStatusCode.Created);

        local.Year.Should().Be(2030);
        local.WorkLocations.Should().ContainSingle().Which.PublicId.Should().Be(dhaka.WorkLocation.PublicId);
        everywhere.WorkLocations.Should().BeEmpty();

        var inDhaka = await ListAsync(hrManager, 2030, dhaka.WorkLocation.PublicId);
        var inChittagong = await ListAsync(hrManager, 2030, chittagong.WorkLocation.PublicId);
        inDhaka.Select(h => h.PublicId).Should().Contain([local.PublicId, everywhere.PublicId]);
        inChittagong.Select(h => h.PublicId).Should().Contain(everywhere.PublicId).And.NotContain(local.PublicId);
        (await ListAsync(hrManager, 2030, workLocationId: null)).Select(h => h.PublicId).Should().Contain([local.PublicId, everywhere.PublicId]);
        (await ListAsync(hrManager, 2031, workLocationId: null)).Should().NotContain(h => h.PublicId == local.PublicId);
    }

    [Fact]
    public async Task A_location_can_be_unlisted_and_listed_again()
    {
        var first = await api.CreateOrgAsync();
        var second = await api.CreateOrgAsync();
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var name = $"Regional Day {Guid.NewGuid():N}";
        var holiday = await (await hrManager.PostAsync("/api/holidays", new
        {
            name, holidayDate = "2030-03-10", workLocationIds = new[] { first.WorkLocation.PublicId, second.WorkLocation.PublicId },
        })).ReadAsync<HolidayDto>(HttpStatusCode.Created);
        var path = $"/api/holidays/{holiday.PublicId}";

        // Moon-sighted: the date moves a day, and the first location drops out.
        var moved = await (await hrManager.PutAsync(path, new
        {
            name, holidayDate = "2030-03-11", workLocationIds = new[] { second.WorkLocation.PublicId },
        })).ReadAsync<HolidayDto>();
        var relisted = await (await hrManager.PutAsync(path, new
        {
            name, holidayDate = "2030-03-11", workLocationIds = new[] { first.WorkLocation.PublicId, second.WorkLocation.PublicId },
        })).ReadAsync<HolidayDto>();

        moved.HolidayDate.Should().Be(new DateOnly(2030, 3, 11));
        moved.WorkLocations.Select(l => l.PublicId).Should().Equal(second.WorkLocation.PublicId);
        relisted.WorkLocations.Select(l => l.PublicId).Should().BeEquivalentTo([first.WorkLocation.PublicId, second.WorkLocation.PublicId]);

        // The unlisted link was soft-deleted and then restored: still two rows, both live.
        var links = await api.QueryDbAsync(db => db.HolidayLocations.IgnoreQueryFilters()
            .Where(l => l.Holiday.PublicId == holiday.PublicId).ToListAsync(Ct));
        links.Should().HaveCount(2).And.OnlyContain(l => !l.IsDeleted);
    }

    [Fact]
    public async Task The_same_holiday_cannot_be_entered_twice_on_a_date_until_it_is_removed()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var body = new { name = $"Duplicate Day {Guid.NewGuid():N}", holidayDate = "2031-02-21" };
        var holiday = await (await hrManager.PostAsync("/api/holidays", body)).ReadAsync<HolidayDto>(HttpStatusCode.Created);

        var duplicate = await hrManager.PostAsync("/api/holidays", body);
        var deleted = await hrManager.DeleteAsync($"/api/holidays/{holiday.PublicId}");
        var afterDelete = await hrManager.GetAsync($"/api/holidays/{holiday.PublicId}");
        var recreated = await hrManager.PostAsync("/api/holidays", body);

        (await duplicate.ReadProblemCodeAsync(HttpStatusCode.Conflict)).Should().Be("holiday.duplicate");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await afterDelete.ReadProblemCodeAsync(HttpStatusCode.NotFound)).Should().Be("holiday.not_found");
        recreated.StatusCode.Should().Be(HttpStatusCode.Created, "a removed holiday no longer counts as a duplicate");
        (await ListAsync(hrManager, 2031, workLocationId: null)).Should().NotContain(h => h.PublicId == holiday.PublicId);
        (await api.QueryDbAsync(db => db.Holidays.IgnoreQueryFilters().AnyAsync(h => h.PublicId == holiday.PublicId && h.IsDeleted, Ct)))
            .Should().BeTrue("holidays are soft-deleted, never erased");
    }

    [Fact]
    public async Task A_holiday_can_only_be_limited_to_active_work_locations()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var org = await api.CreateOrgAsync();
        await hrManager.PostAsync($"/api/work-locations/{org.WorkLocation.PublicId}/deactivate");

        var retired = await hrManager.PostAsync("/api/holidays", new
        {
            name = $"Day {Guid.NewGuid():N}", holidayDate = "2031-05-01", workLocationIds = new[] { org.WorkLocation.PublicId },
        });
        var unknown = await hrManager.PostAsync("/api/holidays", new
        {
            name = $"Day {Guid.NewGuid():N}", holidayDate = "2031-05-01", workLocationIds = new[] { Guid.NewGuid() },
        });

        (await retired.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("holiday.work_location_not_found");
        (await unknown.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("holiday.work_location_not_found");
    }

    [Fact]
    public async Task Any_signed_in_user_can_read_the_calendar_but_only_holiday_write_can_change_it()
    {
        using var hrOfficer = await api.SignInAsync(SystemRoles.HROfficer);
        using var employee = await api.SignInAsync(SystemRoles.Employee);

        (await employee.GetAsync("/api/holidays?year=2031")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await hrOfficer.PostAsync("/api/holidays", new { name = "Day", holidayDate = "2031-06-01" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<List<HolidayDto>> ListAsync(ApiSession session, int year, Guid? workLocationId) =>
        await (await session.GetAsync($"/api/holidays?year={year}{(workLocationId is { } id ? $"&workLocationId={id}" : "")}"))
            .ReadAsync<List<HolidayDto>>();
}
