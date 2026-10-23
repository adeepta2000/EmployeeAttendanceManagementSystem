using System.Net;
using System.Text.Json;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Application.Organization;
using Attendance.Domain.Organization;
using Attendance.Infrastructure.Persistence.Seeding;
using Attendance.Shared.Paging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Tests.Organization;

/// <summary>Employee CRUD (FR-1.2 to FR-1.5) and the list endpoint, as HR uses them.</summary>
[Collection(ApiCollection.Name)]
public sealed class EmployeesApiTests(AttendanceApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HR_creates_an_employee_without_a_login_account()
    {
        var org = await api.CreateOrgAsync();
        var manager = await api.CreateEmployeeAsync(org, firstName: "Nusrat");
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);
        var code = AttendanceApiFactory.UniqueCode("e");

        var response = await hr.PostAsync("/api/employees", Body(org, code, "  Rafiq ") with
        {
            Email = "Rafiq.Islam@Nordwin.Example",
            ReportingManagerId = manager.PublicId,
        });

        var created = await response.ReadAsync<EmployeeDto>(HttpStatusCode.Created);
        created.EmployeeCode.Should().Be(code.ToUpperInvariant());
        created.FirstName.Should().Be("Rafiq");
        created.FullName.Should().Be("Rafiq Islam");
        created.Email.Should().Be("rafiq.islam@nordwin.example");
        created.Department.PublicId.Should().Be(org.Department.PublicId);
        created.ReportingManager!.PublicId.Should().Be(manager.PublicId);
        created.IsActive.Should().BeTrue();
        created.HasUserAccount.Should().BeFalse("FR-1.3: an employee may exist without a login");

        response.Headers.Location!.AbsolutePath.Should().Be($"/api/employees/{created.PublicId}");
        (await hr.GetAsync(response.Headers.Location.AbsolutePath)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Invalid_fields_are_reported_by_their_JSON_names()
    {
        var org = await api.CreateOrgAsync();
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var response = await hr.PostAsync("/api/employees", Body(org, "bad code!", new string('a', 101)) with
        {
            DateOfBirth = "2015-01-01",
            ConfirmationDate = "2019-01-01",
            Email = "not-an-email",
            DepartmentId = Guid.Empty,
        });

        (await response.ReadValidationErrorKeysAsync()).Should().BeEquivalentTo(
            ["employeeCode", "firstName", "dateOfBirth", "confirmationDate", "email", "departmentId"]);
    }

    [Fact]
    public async Task Enum_values_must_be_names_not_numbers()
    {
        var org = await api.CreateOrgAsync();
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var body = JsonSerializer.SerializeToNode(Body(org, AttendanceApiFactory.UniqueCode("e")), ApiSession.Json)!.AsObject();
        body["gender"] = 2;

        var response = await hr.PostAsync("/api/employees", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_employee_code_can_be_used_only_once_whatever_its_case()
    {
        var org = await api.CreateOrgAsync();
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);
        var code = AttendanceApiFactory.UniqueCode("e");
        await (await hr.PostAsync("/api/employees", Body(org, code))).ReadAsync<EmployeeDto>(HttpStatusCode.Created);

        var duplicate = await hr.PostAsync("/api/employees", Body(org, $" {code.ToLowerInvariant()} "));

        (await duplicate.ReadProblemCodeAsync(HttpStatusCode.Conflict)).Should().Be("employee.code_taken");
    }

    [Fact]
    public async Task A_retired_department_cannot_take_new_employees()
    {
        var org = await api.CreateOrgAsync();
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        (await hrManager.PostAsync($"/api/departments/{org.Department.PublicId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await hrManager.PostAsync("/api/employees", Body(org, AttendanceApiFactory.UniqueCode("e")));

        (await response.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("employee.department_not_found");
    }

    [Fact]
    public async Task HR_updates_an_employee()
    {
        var org = await api.CreateOrgAsync();
        var otherOrg = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var updated = await (await hr.PutAsync($"/api/employees/{employee.PublicId}", Body(otherOrg, employee.EmployeeCode, "Shapla") with
        {
            EmploymentType = "Contract",
            Phone = "+880 1700-000000",
        })).ReadAsync<EmployeeDto>();

        updated.FirstName.Should().Be("Shapla");
        updated.EmploymentType.Should().Be(EmploymentType.Contract);
        updated.Department.PublicId.Should().Be(otherOrg.Department.PublicId);
        updated.Phone.Should().Be("+880 1700-000000");
    }

    [Fact]
    public async Task An_employee_cannot_report_to_themselves_or_to_their_own_report()
    {
        var org = await api.CreateOrgAsync();
        var nusrat = await api.CreateEmployeeAsync(org, firstName: "Nusrat");
        var rafiq = await api.CreateEmployeeAsync(org, nusrat, "Rafiq");
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var self = await hr.PutAsync($"/api/employees/{nusrat.PublicId}", Body(org, nusrat.EmployeeCode) with { ReportingManagerId = nusrat.PublicId });
        var loop = await hr.PutAsync($"/api/employees/{nusrat.PublicId}", Body(org, nusrat.EmployeeCode) with { ReportingManagerId = rafiq.PublicId });

        (await self.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("employee.manager_cycle");
        (await loop.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("employee.manager_cycle");
    }

    [Fact]
    public async Task Deactivation_keeps_the_record_and_blocks_the_employees_sign_in()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        var account = await api.CreateUserAsync(SystemRoles.Employee, employee);
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);
        var path = $"/api/employees/{employee.PublicId}";

        var deactivated = await (await hr.PostAsync($"{path}/deactivate", new { separationDate = "2026-09-30" })).ReadAsync<EmployeeDto>();

        deactivated.IsActive.Should().BeFalse();
        deactivated.SeparationDate.Should().Be(new DateOnly(2026, 9, 30));
        deactivated.HasUserAccount.Should().BeTrue();
        (await hr.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK, "FR-1.5: deactivation never deletes");
        (await api.QueryDbAsync(db => db.Employees.CountAsync(e => e.Id == employee.Id, Ct))).Should().Be(1);

        using var client = api.CreateApiClient();
        (await client.LoginAsync(account.Email)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var again = await hr.PostAsync($"{path}/deactivate", new { separationDate = "2026-09-30" });
        (await again.ReadProblemCodeAsync(HttpStatusCode.Conflict)).Should().Be("employee.already_inactive");
    }

    [Fact]
    public async Task Separation_cannot_precede_joining()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org, joiningDate: new DateOnly(2024, 3, 1));
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var response = await hr.PostAsync($"/api/employees/{employee.PublicId}/deactivate", new { separationDate = "2024-02-28" });

        (await response.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("employee.separation_before_joining");
    }

    [Fact]
    public async Task Reactivation_clears_the_separation_date()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org, isActive: false);
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var reactivated = await (await hr.PostAsync($"/api/employees/{employee.PublicId}/reactivate")).ReadAsync<EmployeeDto>();

        reactivated.IsActive.Should().BeTrue();
        reactivated.SeparationDate.Should().BeNull();
    }

    [Fact]
    public async Task The_list_filters_by_department_designation_type_status_and_search()
    {
        var org = await api.CreateOrgAsync();
        var rafiq = await api.CreateEmployeeAsync(org, firstName: "Rafiq", lastName: "Islam");
        var shapla = await api.CreateEmployeeAsync(org, firstName: "Shapla", lastName: "Akter", employmentType: EmploymentType.Contract);
        var karim = await api.CreateEmployeeAsync(org, firstName: "Karim", lastName: "Uddin", isActive: false);
        await api.CreateEmployeeAsync(await api.CreateOrgAsync(), firstName: "Rafiq", lastName: "Islam");
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);
        var inDepartment = $"/api/employees?departmentId={org.Department.PublicId}";

        (await CodesAsync(hr, inDepartment)).Should().BeEquivalentTo([rafiq.EmployeeCode, shapla.EmployeeCode, karim.EmployeeCode]);
        (await CodesAsync(hr, $"/api/employees?designationId={org.Designation.PublicId}&isActive=true"))
            .Should().BeEquivalentTo([rafiq.EmployeeCode, shapla.EmployeeCode]);
        (await CodesAsync(hr, $"{inDepartment}&isActive=false")).Should().Equal(karim.EmployeeCode);
        (await CodesAsync(hr, $"{inDepartment}&employmentType=Contract")).Should().Equal(shapla.EmployeeCode);
        (await CodesAsync(hr, $"{inDepartment}&search=fiq isl")).Should().Equal(rafiq.EmployeeCode);
        (await CodesAsync(hr, $"{inDepartment}&search=akter")).Should().Equal(shapla.EmployeeCode);
        (await CodesAsync(hr, $"{inDepartment}&search={karim.EmployeeCode[2..10]}")).Should().Equal(karim.EmployeeCode);
        (await CodesAsync(hr, $"{inDepartment}&search=%25")).Should().BeEmpty("LIKE wildcards in the term are matched literally");
    }

    [Fact]
    public async Task The_list_sorts_and_pages()
    {
        var org = await api.CreateOrgAsync();
        foreach (var name in new[] { "Bina", "Dipa", "Anjali", "Champa", "Ella" })
        {
            await api.CreateEmployeeAsync(org, firstName: name);
        }

        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var page = await (await hr.GetAsync(
            $"/api/employees?departmentId={org.Department.PublicId}&sortBy=Name&sortDirection=Desc&page=2&pageSize=2"))
            .ReadAsync<PagedResult<EmployeeSummaryDto>>();

        page.Items.Select(e => e.FirstName).Should().Equal("Champa", "Bina");
        page.Page.Should().Be(2);
        page.TotalCount.Should().Be(5);
        page.TotalPages.Should().Be(3);
    }

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    [InlineData("sortBy=Salary")]
    [InlineData("employmentType=Freelance")]
    public async Task Invalid_list_parameters_are_rejected(string query)
    {
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var response = await hr.GetAsync($"/api/employees?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<List<string>> CodesAsync(ApiSession session, string path) =>
        [.. (await (await session.GetAsync(path)).ReadAsync<PagedResult<EmployeeSummaryDto>>()).Items.Select(e => e.EmployeeCode)];

    private static EmployeeBody Body(TestOrg org, string code, string firstName = "Rafiq") => new(
        code, firstName, "Islam", "1994-02-11", "Male", null, null, "2021-07-01", "2022-01-01",
        "Permanent", org.Department.PublicId, org.Designation.PublicId, org.WorkLocation.PublicId, null);
}
