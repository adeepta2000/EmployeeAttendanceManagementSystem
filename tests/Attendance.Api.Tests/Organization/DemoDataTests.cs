using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Application.Organization;
using Attendance.Infrastructure.Persistence.Seeding;
using Attendance.Infrastructure.Persistence.Seeding.Demo;
using Attendance.Shared.Paging;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Tests.Organization;

/// <summary>The API started with demo data enabled, on a database of its own.</summary>
public sealed class DemoDataApiFactory : AttendanceApiFactory
{
    public const string DemoPassword = "Demo-Data-Pa55word";

    protected override void ConfigureSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("DemoData:Enabled", "true");
        builder.UseSetting("DemoData:Password", DemoPassword);
    }
}

/// <summary>
/// The demo company as the startup guard seeds it into SQL Server, and data scoping over it: the seeded
/// managers see exactly their own teams.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DemoDataTests(DemoDataApiFactory demo) : IClassFixture<DemoDataApiFactory>
{
    private const string Nusrat = "nusrat.jahan@" + NordwinDemoData.EmailDomain;
    private const string Farhana = "farhana.rahman@" + NordwinDemoData.EmailDomain;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Startup_seeds_the_company_with_the_requested_shape()
    {
        var shape = await demo.QueryDbAsync(async db => new
        {
            Departments = await db.Departments.CountAsync(Ct),
            Designations = await db.Designations.CountAsync(Ct),
            Locations = await db.WorkLocations.OrderBy(l => l.Code).Select(l => l.Name).ToListAsync(Ct),
            Employees = await db.Employees.CountAsync(Ct),
            Codes = await db.Employees.Select(e => e.EmployeeCode).ToListAsync(Ct),
            EmployeesWithoutLogin = await db.Employees.CountAsync(e => !db.Users.Any(u => u.EmployeeId == e.Id), Ct),
            Roles = await db.UserRoles.GroupBy(ur => ur.Role.Name).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, Ct),
            AdminHasNoEmployee = await db.Users.AnyAsync(u => u.Email == DemoDataSeeder.AdminEmail && u.EmployeeId == null, Ct),
            DepartmentsWithoutHead = await db.Departments.CountAsync(d => d.HeadEmployeeId == null, Ct),
        });

        shape.Departments.Should().Be(6);
        shape.Designations.Should().Be(12);
        shape.Locations.Should().Equal("Chittagong Unit", "Dhaka Unit");
        shape.Employees.Should().Be(120);
        shape.Codes.Should().BeEquivalentTo(Enumerable.Range(1, 120).Select(n => $"NW-{n:D3}"));
        shape.EmployeesWithoutLogin.Should().Be(0);
        shape.Roles.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            [SystemRoles.Admin] = 1,
            [SystemRoles.HRManager] = 1,
            [SystemRoles.HROfficer] = 2,
            [SystemRoles.Manager] = 8,
            [SystemRoles.Employee] = 109,
        });
        shape.AdminHasNoEmployee.Should().BeTrue();
        shape.DepartmentsWithoutHead.Should().Be(0);
    }

    [Fact]
    public async Task A_seeded_manager_sees_exactly_their_own_team()
    {
        using var client = demo.CreateApiClient();
        var tokens = await (await client.LoginAsync(Nusrat, DemoDataApiFactory.DemoPassword)).ReadTokensAsync();
        using var nusrat = new ApiSession(client, tokens.AccessToken, new TestUser(Nusrat, Guid.Empty));

        var team = await (await nusrat.GetAsync("/api/employees?pageSize=100")).ReadAsync<PagedResult<EmployeeSummaryDto>>();
        var farhanaId = await demo.QueryDbAsync(db => db.Users.Where(u => u.Email == Farhana).Select(u => u.Employee!.PublicId).SingleAsync(Ct));

        team.TotalCount.Should().Be(25, "Nusrat and the 24 people on her sewing floor");
        team.Items.Should().ContainSingle(e => e.FullName == "Rafiq Islam");
        team.Items.Where(e => e.FullName != "Nusrat Jahan").Should().OnlyContain(e => e.ReportingManager!.FullName == "Nusrat Jahan");
        (await nusrat.GetAsync($"/api/employees/{farhanaId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_seeded_HR_manager_sees_the_whole_company()
    {
        using var client = demo.CreateApiClient();
        var tokens = await (await client.LoginAsync(Farhana, DemoDataApiFactory.DemoPassword)).ReadTokensAsync();
        using var farhana = new ApiSession(client, tokens.AccessToken, new TestUser(Farhana, Guid.Empty));

        var everyone = await (await farhana.GetAsync("/api/employees?pageSize=1")).ReadAsync<PagedResult<EmployeeSummaryDto>>();
        var leavers = await (await farhana.GetAsync("/api/employees?pageSize=100&isActive=false")).ReadAsync<PagedResult<EmployeeSummaryDto>>();

        everyone.TotalCount.Should().Be(120);
        leavers.TotalCount.Should().Be(3);
    }
}
