using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Application.Organization;
using Attendance.Domain.Identity;
using Attendance.Infrastructure.Persistence.Seeding;
using Attendance.Shared.Paging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Tests.Organization;

/// <summary>
/// Data scoping (FR-8.3) and IDOR defence on every <c>{publicId}</c> route (spec §2.5). An employee
/// outside the caller's scope must look exactly like one that doesn't exist: 404, never 403, which
/// would confirm it exists. 403 is reserved for a missing permission.
/// </summary>
/// <remarks>
/// Org chart, arrows pointing to the reporting manager:
/// Rafiq → Nusrat → Farhana ← Tanvir; Karim (inactive) → Nusrat; Jesmin → Monir.
/// </remarks>
[Collection(ApiCollection.Name)]
public sealed class EmployeeScopingTests(AttendanceApiFactory api)
{
    [Fact]
    public async Task A_manager_gets_404_not_403_for_an_employee_outside_their_reporting_tree()
    {
        var chart = await CreateOrgChartAsync();
        using var nusrat = await api.SignInAsync(SystemRoles.Manager, chart.Nusrat);

        // Another manager's team, Nusrat's own manager, and her peer: all outside her tree.
        foreach (var outsider in new[] { chart.Jesmin, chart.Monir, chart.Farhana, chart.Tanvir })
        {
            var response = await nusrat.GetAsync($"/api/employees/{outsider.PublicId}");

            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
            (await response.ReadProblemCodeAsync(HttpStatusCode.NotFound)).Should().Be("employee.not_found");
        }
    }

    [Fact]
    public async Task An_out_of_scope_employee_is_indistinguishable_from_a_nonexistent_one()
    {
        var chart = await CreateOrgChartAsync();
        using var nusrat = await api.SignInAsync(SystemRoles.Manager, chart.Nusrat);

        var outOfScope = await (await nusrat.GetAsync($"/api/employees/{chart.Jesmin.PublicId}")).ReadProblemAsync(HttpStatusCode.NotFound);
        var nonexistent = await (await nusrat.GetAsync($"/api/employees/{Guid.NewGuid()}")).ReadProblemAsync(HttpStatusCode.NotFound);

        // Everything but the per-request correlation ID matches.
        (outOfScope.Type, outOfScope.Title, outOfScope.Detail, outOfScope.Extensions["code"]!.ToString())
            .Should().Be((nonexistent.Type, nonexistent.Title, nonexistent.Detail, nonexistent.Extensions["code"]!.ToString()));
    }

    [Fact]
    public async Task A_manager_reads_themselves_and_their_whole_tree_including_indirect_and_inactive_reports()
    {
        var chart = await CreateOrgChartAsync();
        using var nusrat = await api.SignInAsync(SystemRoles.Manager, chart.Nusrat);
        using var farhana = await api.SignInAsync(SystemRoles.Manager, chart.Farhana);

        foreach (var visible in new[] { chart.Nusrat, chart.Rafiq, chart.Karim })
        {
            var employee = await (await nusrat.GetAsync($"/api/employees/{visible.PublicId}")).ReadAsync<EmployeeDto>();
            employee.EmployeeCode.Should().Be(visible.EmployeeCode);
        }

        // Rafiq reports to Nusrat, who reports to Farhana: an indirect report.
        (await farhana.GetAsync($"/api/employees/{chart.Rafiq.PublicId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_managers_list_holds_exactly_themselves_and_their_reporting_tree()
    {
        var chart = await CreateOrgChartAsync();
        using var nusrat = await api.SignInAsync(SystemRoles.Manager, chart.Nusrat);
        using var farhana = await api.SignInAsync(SystemRoles.Manager, chart.Farhana);

        var nusratsList = await ListAllAsync(nusrat);
        var farhanasList = await ListAllAsync(farhana);

        nusratsList.Items.Select(e => e.PublicId).Should().BeEquivalentTo(
            new[] { chart.Nusrat, chart.Rafiq, chart.Karim }.Select(e => e.PublicId));
        nusratsList.TotalCount.Should().Be(3);
        farhanasList.Items.Select(e => e.PublicId).Should().BeEquivalentTo(
            new[] { chart.Farhana, chart.Nusrat, chart.Tanvir, chart.Rafiq, chart.Karim }.Select(e => e.PublicId));
    }

    [Fact]
    public async Task An_employee_sees_only_themselves()
    {
        var chart = await CreateOrgChartAsync();
        using var rafiq = await api.SignInAsync(SystemRoles.Employee, chart.Rafiq);

        (await rafiq.GetAsync($"/api/employees/{chart.Rafiq.PublicId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await rafiq.GetAsync($"/api/employees/{chart.Nusrat.PublicId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await rafiq.GetAsync($"/api/employees/{chart.Karim.PublicId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await ListAllAsync(rafiq);
        list.Items.Should().ContainSingle().Which.PublicId.Should().Be(chart.Rafiq.PublicId);
    }

    [Fact]
    public async Task HR_sees_the_whole_company()
    {
        var chart = await CreateOrgChartAsync();
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        (await hr.GetAsync($"/api/employees/{chart.Jesmin.PublicId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await hr.GetAsync($"/api/employees/{chart.Rafiq.PublicId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var department = await (await hr.GetAsync($"/api/employees?pageSize=100&departmentId={chart.Org.Department.PublicId}"))
            .ReadAsync<PagedResult<EmployeeSummaryDto>>();
        department.TotalCount.Should().Be(7);
    }

    [Fact]
    public async Task A_caller_with_no_employee_record_and_no_company_scope_sees_nobody()
    {
        var chart = await CreateOrgChartAsync();
        using var caller = await api.SignInAsync(await api.CreateRoleAsync(Permissions.Employee.Read));

        (await ListAllAsync(caller)).Items.Should().BeEmpty();
        (await caller.GetAsync($"/api/employees/{chart.Rafiq.PublicId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Writes_are_scoped_like_reads()
    {
        var chart = await CreateOrgChartAsync();

        // Write access without company scope: e.g. an admin granted a manager employee.write on screen 33.
        var role = await api.CreateRoleAsync(Permissions.Employee.Read, Permissions.Employee.Write);
        using var nusrat = await api.SignInAsync(role, chart.Nusrat);
        var jesmin = $"/api/employees/{chart.Jesmin.PublicId}";

        (await nusrat.PutAsync(jesmin, ValidBody(chart, "Jesmin"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await nusrat.PostAsync($"{jesmin}/deactivate", new { separationDate = "2026-09-30" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await nusrat.PostAsync($"/api/employees/{chart.Karim.PublicId}/reactivate")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await nusrat.PostAsync($"/api/employees/{chart.Rafiq.PublicId}/deactivate", new { separationDate = "2026-09-30" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var jesminIsActive = await api.QueryDbAsync(db => db.Employees.Where(e => e.Id == chart.Jesmin.Id).Select(e => e.IsActive).SingleAsync());
        jesminIsActive.Should().BeTrue("a write outside the caller's scope must change nothing");
    }

    [Fact]
    public async Task A_reporting_manager_outside_the_callers_scope_cannot_be_assigned()
    {
        var chart = await CreateOrgChartAsync();
        var role = await api.CreateRoleAsync(Permissions.Employee.Read, Permissions.Employee.Write);
        using var nusrat = await api.SignInAsync(role, chart.Nusrat);

        var response = await nusrat.PutAsync(
            $"/api/employees/{chart.Rafiq.PublicId}", ValidBody(chart, "Rafiq") with { ReportingManagerId = chart.Monir.PublicId });

        (await response.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("employee.manager_not_found");
    }

    [Fact]
    public async Task A_missing_permission_is_403_even_for_an_employee_in_scope()
    {
        var chart = await CreateOrgChartAsync();
        using var nusrat = await api.SignInAsync(SystemRoles.Manager, chart.Nusrat);

        var update = await nusrat.PutAsync($"/api/employees/{chart.Rafiq.PublicId}", ValidBody(chart, "Rafiq"));
        var create = await nusrat.PostAsync("/api/employees", ValidBody(chart, "New"));

        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<PagedResult<EmployeeSummaryDto>> ListAllAsync(ApiSession session) =>
        await (await session.GetAsync("/api/employees?pageSize=100")).ReadAsync<PagedResult<EmployeeSummaryDto>>();

    private static EmployeeBody ValidBody(OrgChart chart, string firstName) => new(
        AttendanceApiFactory.UniqueCode("E"), firstName, "Test", "1990-05-17", "Female", null, null, "2020-01-01", null,
        "Permanent", chart.Org.Department.PublicId, chart.Org.Designation.PublicId, chart.Org.WorkLocation.PublicId, null);

    private async Task<OrgChart> CreateOrgChartAsync()
    {
        var org = await api.CreateOrgAsync();
        var farhana = await api.CreateEmployeeAsync(org, firstName: "Farhana");
        var nusrat = await api.CreateEmployeeAsync(org, farhana, "Nusrat");
        var tanvir = await api.CreateEmployeeAsync(org, farhana, "Tanvir");
        var rafiq = await api.CreateEmployeeAsync(org, nusrat, "Rafiq");
        var karim = await api.CreateEmployeeAsync(org, nusrat, "Karim", isActive: false);
        var monir = await api.CreateEmployeeAsync(org, firstName: "Monir");
        var jesmin = await api.CreateEmployeeAsync(org, monir, "Jesmin");
        return new OrgChart(org, farhana, nusrat, tanvir, rafiq, karim, monir, jesmin);
    }

    private sealed record OrgChart(
        TestOrg Org,
        TestEmployee Farhana,
        TestEmployee Nusrat,
        TestEmployee Tanvir,
        TestEmployee Rafiq,
        TestEmployee Karim,
        TestEmployee Monir,
        TestEmployee Jesmin);
}

/// <summary>The JSON body of POST and PUT <c>/api/employees</c>, as a client sends it.</summary>
internal sealed record EmployeeBody(
    string EmployeeCode,
    string FirstName,
    string LastName,
    string DateOfBirth,
    string Gender,
    string? Email,
    string? Phone,
    string JoiningDate,
    string? ConfirmationDate,
    string EmploymentType,
    Guid DepartmentId,
    Guid DesignationId,
    Guid WorkLocationId,
    Guid? ReportingManagerId);
