using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Application.Organization;
using Attendance.Infrastructure.Persistence.Seeding;
using FluentAssertions;

namespace Attendance.Api.Tests.Organization;

/// <summary>Departments, designations and work locations (FR-1.1).</summary>
[Collection(ApiCollection.Name)]
public sealed class MasterDataApiTests(AttendanceApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HR_manager_creates_a_department_with_a_normalized_code()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var code = AttendanceApiFactory.UniqueCode("d").ToLowerInvariant();

        var response = await hrManager.PostAsync("/api/departments", new { code = $" {code} ", name = " Knitting " });

        var created = await response.ReadAsync<DepartmentDto>(HttpStatusCode.Created);
        created.Code.Should().Be(code.ToUpperInvariant());
        created.Name.Should().Be("Knitting");
        created.IsActive.Should().BeTrue();
        response.Headers.Location!.AbsolutePath.Should().Be($"/api/departments/{created.PublicId}");
    }

    [Fact]
    public async Task Any_signed_in_user_can_read_master_data_but_only_org_write_can_change_it()
    {
        var org = await api.CreateOrgAsync();
        using var employee = await api.SignInAsync(SystemRoles.Employee);
        using var anonymous = api.CreateApiClient();

        (await employee.GetAsync("/api/departments")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await employee.GetAsync($"/api/designations/{org.Designation.PublicId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await employee.GetAsync("/api/work-locations?isActive=true")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await employee.PostAsync("/api/departments", new { code = "X1", name = "X" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.GetAsync("/api/departments", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_department_code_can_be_used_only_once()
    {
        var org = await api.CreateOrgAsync();
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);

        var response = await hrManager.PostAsync("/api/departments", new { code = org.Department.Code.ToLowerInvariant(), name = "Copy" });

        (await response.ReadProblemCodeAsync(HttpStatusCode.Conflict)).Should().Be("department.code_taken");
    }

    [Fact]
    public async Task A_department_cannot_sit_under_itself_or_its_own_sub_department()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var parent = await CreateDepartmentAsync(hrManager, parentId: null);
        var child = await CreateDepartmentAsync(hrManager, parent.PublicId);

        var underItself = await hrManager.PutAsync($"/api/departments/{parent.PublicId}", new { code = parent.Code, name = parent.Name, parentDepartmentId = parent.PublicId });
        var underChild = await hrManager.PutAsync($"/api/departments/{parent.PublicId}", new { code = parent.Code, name = parent.Name, parentDepartmentId = child.PublicId });

        (await underItself.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("department.parent_cycle");
        (await underChild.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("department.parent_cycle");
        child.ParentDepartment!.PublicId.Should().Be(parent.PublicId);
    }

    [Fact]
    public async Task A_department_head_must_be_an_active_employee()
    {
        var org = await api.CreateOrgAsync();
        var head = await api.CreateEmployeeAsync(org, firstName: "Farhana", lastName: "Rahman");
        var leaver = await api.CreateEmployeeAsync(org, isActive: false);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var path = $"/api/departments/{org.Department.PublicId}";

        var updated = await (await hrManager.PutAsync(path, new { code = org.Department.Code, name = "Sewing", headEmployeeId = head.PublicId }))
            .ReadAsync<DepartmentDto>();
        var rejected = await hrManager.PutAsync(path, new { code = org.Department.Code, name = "Sewing", headEmployeeId = leaver.PublicId });

        updated.HeadEmployee.Should().Be(new EmployeeRef(head.PublicId, head.EmployeeCode, "Farhana Rahman"));
        (await rejected.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("department.head_not_found");
    }

    [Fact]
    public async Task Master_data_in_use_by_active_employees_cannot_be_retired()
    {
        var org = await api.CreateOrgAsync();
        var employee = await api.CreateEmployeeAsync(org);
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);

        (await (await hrManager.PostAsync($"/api/departments/{org.Department.PublicId}/deactivate")).ReadProblemCodeAsync(HttpStatusCode.Conflict))
            .Should().Be("department.in_use");
        (await (await hrManager.PostAsync($"/api/designations/{org.Designation.PublicId}/deactivate")).ReadProblemCodeAsync(HttpStatusCode.Conflict))
            .Should().Be("designation.in_use");
        (await (await hrManager.PostAsync($"/api/work-locations/{org.WorkLocation.PublicId}/deactivate")).ReadProblemCodeAsync(HttpStatusCode.Conflict))
            .Should().Be("work_location.in_use");

        await hrManager.PostAsync($"/api/employees/{employee.PublicId}/deactivate", new { separationDate = "2026-09-30" });
        var retired = await (await hrManager.PostAsync($"/api/departments/{org.Department.PublicId}/deactivate")).ReadAsync<DepartmentDto>();
        retired.IsActive.Should().BeFalse();

        var activeOnly = await (await hrManager.GetAsync("/api/departments?isActive=true")).ReadAsync<List<DepartmentDto>>();
        activeOnly.Should().NotContain(d => d.PublicId == org.Department.PublicId);
    }

    [Fact]
    public async Task HR_manager_maintains_designations()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);
        var code = AttendanceApiFactory.UniqueCode("g");

        var created = await (await hrManager.PostAsync("/api/designations", new { code, name = "Sewing Operator", grade = "W-4" }))
            .ReadAsync<DesignationDto>(HttpStatusCode.Created);
        var updated = await (await hrManager.PutAsync($"/api/designations/{created.PublicId}", new { code, name = "Senior Sewing Operator", grade = " " }))
            .ReadAsync<DesignationDto>();

        updated.Should().Be(created with { Name = "Senior Sewing Operator", Grade = null });
    }

    [Fact]
    public async Task A_work_location_geofence_needs_both_coordinates()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);

        var latitudeOnly = await hrManager.PostAsync("/api/work-locations", new { code = AttendanceApiFactory.UniqueCode("l"), name = "Gazipur", latitude = 23.99m });
        var radiusOnly = await hrManager.PostAsync("/api/work-locations", new { code = AttendanceApiFactory.UniqueCode("l"), name = "Gazipur", geofenceRadiusMeters = 250 });
        var tooPrecise = await hrManager.PostAsync("/api/work-locations", new
        {
            code = AttendanceApiFactory.UniqueCode("l"), name = "Gazipur", latitude = 23.9999991m, longitude = 90.4m,
        });
        var valid = await hrManager.PostAsync("/api/work-locations", new
        {
            code = AttendanceApiFactory.UniqueCode("l"), name = "Gazipur", latitude = 23.999123m, longitude = 90.412521m, geofenceRadiusMeters = 250,
        });

        (await latitudeOnly.ReadValidationErrorKeysAsync()).Should().Contain("longitude");
        (await radiusOnly.ReadValidationErrorKeysAsync()).Should().Contain("geofenceRadiusMeters");
        (await tooPrecise.ReadValidationErrorKeysAsync()).Should().Contain("latitude");
        var created = await valid.ReadAsync<WorkLocationDto>(HttpStatusCode.Created);
        created.Latitude.Should().Be(23.999123m);
        created.GeofenceRadiusMeters.Should().Be(250);
    }

    [Fact]
    public async Task An_unknown_public_id_is_404()
    {
        using var hrManager = await api.SignInAsync(SystemRoles.HRManager);

        (await (await hrManager.GetAsync($"/api/departments/{Guid.NewGuid()}")).ReadProblemCodeAsync(HttpStatusCode.NotFound))
            .Should().Be("department.not_found");
        (await (await hrManager.PostAsync($"/api/designations/{Guid.NewGuid()}/activate")).ReadProblemCodeAsync(HttpStatusCode.NotFound))
            .Should().Be("designation.not_found");
    }

    private static async Task<DepartmentDto> CreateDepartmentAsync(ApiSession session, Guid? parentId) =>
        await (await session.PostAsync("/api/departments", new
        {
            code = AttendanceApiFactory.UniqueCode("d"),
            name = "Department",
            parentDepartmentId = parentId,
        })).ReadAsync<DepartmentDto>(HttpStatusCode.Created);
}
