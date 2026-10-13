using Attendance.Application.Common.Exceptions;
using Attendance.Application.Tests.Persistence;
using Attendance.Domain.Identity;
using Attendance.Domain.Organization;
using Attendance.Infrastructure.Identity;
using FluentAssertions;

namespace Attendance.Application.Tests.Identity;

/// <summary>
/// Org chart used below (arrows point to the reporting manager):
/// Rafiq → Nusrat → Farhana ← Tanvir, Karim (inactive) → Nusrat, and Shila with no manager.
/// </summary>
public sealed class UserContextServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly Dictionary<string, Employee> _employees = [];

    public void Dispose() => _db.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Resolves_the_callers_internal_ids_and_token_permissions()
    {
        await SeedOrgChartAsync();
        var user = await AddUserAsync(employee: "Nusrat");
        _db.CurrentUser.Permissions = [Permissions.Leave.Approve, Permissions.Overtime.Approve];

        var context = await CreateService().GetCurrentAsync(Ct);

        context.UserId.Should().Be(user.Id);
        context.UserPublicId.Should().Be(user.PublicId);
        context.EmployeeId.Should().Be(_employees["Nusrat"].Id);
        context.HasPermission(Permissions.Leave.Approve).Should().BeTrue();
        context.HasPermission(Permissions.Report.Export).Should().BeFalse();
    }

    [Theory]
    [InlineData("Farhana", new[] { "Nusrat", "Tanvir", "Rafiq", "Karim" })]
    [InlineData("Nusrat", new[] { "Rafiq", "Karim" })]
    [InlineData("Rafiq", new string[0])]
    [InlineData("Shila", new string[0])]
    public async Task The_reporting_tree_holds_direct_and_indirect_reports(string caller, string[] expected)
    {
        await SeedOrgChartAsync();
        await AddUserAsync(employee: caller);

        var tree = await CreateService().GetReportingTreeAsync(Ct);

        tree.Should().BeEquivalentTo(expected.Select(name => _employees[name].Id));
    }

    [Fact]
    public async Task A_user_without_an_employee_record_has_an_empty_tree()
    {
        await SeedOrgChartAsync();
        await AddUserAsync(employee: null);

        var service = CreateService();

        (await service.GetCurrentAsync(Ct)).EmployeeId.Should().BeNull();
        (await service.GetReportingTreeAsync(Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_reporting_cycle_does_not_loop_forever()
    {
        await SeedOrgChartAsync();
        await using (var context = _db.CreateContext())
        {
            // Bad data: Farhana now reports to Rafiq, who is in her own tree.
            var farhana = await context.Employees.FindAsync([_employees["Farhana"].Id], Ct);
            farhana!.ReportingManagerId = _employees["Rafiq"].Id;
            await context.SaveChangesAsync(Ct);
        }

        await AddUserAsync(employee: "Nusrat");

        var tree = await CreateService().GetReportingTreeAsync(Ct);

        tree.Should().BeEquivalentTo(new[] { "Rafiq", "Karim", "Farhana", "Tanvir" }.Select(name => _employees[name].Id));
    }

    [Fact]
    public async Task Nobody_signed_in_is_unauthenticated()
    {
        _db.CurrentUser.UserId = null;

        var act = () => CreateService().GetCurrentAsync(Ct);

        await act.Should().ThrowAsync<UnauthenticatedException>();
    }

    [Fact]
    public async Task A_deactivated_account_is_unauthenticated_even_with_a_valid_token()
    {
        await SeedOrgChartAsync();
        var user = await AddUserAsync(employee: "Rafiq");
        await using (var context = _db.CreateContext())
        {
            (await context.Users.FindAsync([user.Id], Ct))!.IsActive = false;
            await context.SaveChangesAsync(Ct);
        }

        var act = () => CreateService().GetCurrentAsync(Ct);

        await act.Should().ThrowAsync<UnauthenticatedException>();
    }

    private UserContextService CreateService() => new(_db.CurrentUser, _db.CreateContext());

    private async Task<User> AddUserAsync(string? employee)
    {
        await using var context = _db.CreateContext();
        var user = new User
        {
            Email = $"{employee ?? "it.admin"}@nordwin.example",
            PasswordHash = "hash",
            EmployeeId = employee is null ? null : _employees[employee].Id,
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);

        _db.CurrentUser.UserId = user.PublicId.ToString();
        return user;
    }

    private async Task SeedOrgChartAsync()
    {
        await using var context = _db.CreateContext();
        var department = new Department { Code = "SEW", Name = "Sewing" };
        var designation = new Designation { Code = "OPR", Name = "Operator" };
        var location = new WorkLocation { Code = "DHK", Name = "Dhaka Factory" };
        context.AddRange(department, designation, location);
        await context.SaveChangesAsync(Ct);

        async Task AddAsync(string name, string? manager, bool isActive = true)
        {
            var employee = new Employee
            {
                EmployeeCode = $"NW-{_employees.Count + 1:D4}",
                FirstName = name,
                LastName = "Test",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Gender = Gender.Other,
                JoiningDate = new DateOnly(2020, 1, 1),
                EmploymentType = EmploymentType.Permanent,
                IsActive = isActive,
                DepartmentId = department.Id,
                DesignationId = designation.Id,
                WorkLocationId = location.Id,
                ReportingManagerId = manager is null ? null : _employees[manager].Id,
            };
            context.Employees.Add(employee);
            await context.SaveChangesAsync(Ct);
            _employees[name] = employee;
        }

        await AddAsync("Farhana", manager: null);
        await AddAsync("Nusrat", manager: "Farhana");
        await AddAsync("Tanvir", manager: "Farhana");
        await AddAsync("Rafiq", manager: "Nusrat");
        await AddAsync("Karim", manager: "Nusrat", isActive: false);
        await AddAsync("Shila", manager: null);
    }
}
