using Attendance.Application.Organization.Employees;
using Attendance.Domain.Organization;
using Attendance.Infrastructure.Persistence.Seeding;
using Attendance.Infrastructure.Persistence.Seeding.Demo;
using FluentAssertions;

namespace Attendance.Application.Tests.Organization;

/// <summary>The demo company generator, without a database.</summary>
public sealed class NordwinDemoDataTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 1);

    private static readonly HashSet<string> EnglishFirstNames =
    [
        "James", "Oliver", "Daniel", "Thomas", "William", "Henry", "Edward", "George", "Michael", "David",
        "Emily", "Charlotte", "Sophie", "Olivia", "Grace", "Hannah", "Lucy", "Amelia", "Rachel", "Laura",
    ];

    private readonly DemoDataSet _data = NordwinDemoData.Generate(AsOf);

    private IEnumerable<Employee> Employees => _data.Employees.Select(e => e.Employee);

    [Fact]
    public void Has_six_departments_twelve_designations_and_the_two_work_locations()
    {
        _data.Departments.Should().HaveCount(6);
        _data.Designations.Should().HaveCount(12);
        _data.WorkLocations.Select(l => (l.Code, l.Name)).Should().BeEquivalentTo(
            [("DHK", "Dhaka Unit"), ("CTG", "Chittagong Unit")]);
        _data.WorkLocations.Should().OnlyContain(l => l.Latitude != null && l.Longitude != null && l.GeofenceRadiusMeters > 0);
    }

    [Fact]
    public void Has_120_employees_with_codes_NW_001_to_NW_120_in_joining_order()
    {
        var byCode = Employees.OrderBy(e => e.EmployeeCode, StringComparer.Ordinal).ToList();

        byCode.Select(e => e.EmployeeCode).Should().Equal(Enumerable.Range(1, 120).Select(n => $"NW-{n:D3}"));
        byCode.Select(e => e.JoiningDate).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Roles_are_8_managers_1_HR_manager_2_HR_officers_and_employees_for_the_rest()
    {
        _data.Employees.GroupBy(e => e.Role).ToDictionary(g => g.Key, g => g.Count()).Should().BeEquivalentTo(
            new Dictionary<string, int>
            {
                [SystemRoles.Manager] = 8,
                [SystemRoles.HRManager] = 1,
                [SystemRoles.HROfficer] = 2,
                [SystemRoles.Employee] = 109,
            });
    }

    [Fact]
    public void The_reporting_hierarchy_is_exactly_three_levels_deep_under_one_general_manager()
    {
        static int Level(Employee e) => e.ReportingManager is null ? 1 : Level(e.ReportingManager) + 1;

        var levels = Employees.GroupBy(Level).ToDictionary(g => g.Key, g => g.Count());

        levels.Should().BeEquivalentTo(new Dictionary<int, int> { [1] = 1, [2] = 8, [3] = 111 });
        Employees.Single(e => e.ReportingManager is null).Designation.Code.Should().Be("GM");
    }

    [Fact]
    public void Exactly_the_managers_have_reports()
    {
        var managers = Employees.Where(e => e.ReportingManager is not null).Select(e => e.ReportingManager!).ToHashSet();

        _data.Employees.Where(e => managers.Contains(e.Employee)).Select(e => e.Role).Distinct()
            .Should().BeEquivalentTo([SystemRoles.Manager, SystemRoles.HRManager]);
        managers.Should().HaveCount(9);
    }

    [Fact]
    public void The_spec_personas_are_in_the_right_places()
    {
        var byName = _data.Employees.ToDictionary(e => e.Employee.FullName);

        byName["Farhana Rahman"].Role.Should().Be(SystemRoles.HRManager);
        byName["Tanvir Ahmed"].Role.Should().Be(SystemRoles.HROfficer);
        byName["Tanvir Ahmed"].Employee.ReportingManager.Should().BeSameAs(byName["Farhana Rahman"].Employee);
        byName["Nusrat Jahan"].Role.Should().Be(SystemRoles.Manager);
        byName["Rafiq Islam"].Employee.ReportingManager.Should().BeSameAs(byName["Nusrat Jahan"].Employee);
        byName["Rafiq Islam"].Employee.Email.Should().Be("rafiq.islam@nordwin.example");
    }

    [Fact]
    public void Names_mix_Bengali_and_English()
    {
        var english = Employees.Count(e => EnglishFirstNames.Contains(e.FirstName));

        english.Should().BeInRange(5, 40, "English names are a minority but clearly present");
        Employees.Select(e => e.FullName).Should().OnlyHaveUniqueItems();
        Employees.Select(e => e.Email).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Every_employee_passes_the_same_validation_as_the_API()
    {
        var validator = new CreateEmployeeCommandValidator();

        foreach (var e in Employees)
        {
            var command = new CreateEmployeeCommand(
                e.EmployeeCode, e.FirstName, e.LastName, e.DateOfBirth, e.Gender, e.Email, e.Phone, e.JoiningDate,
                e.ConfirmationDate, e.EmploymentType, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);

            validator.Validate(command).Errors.Should().BeEmpty(e.EmployeeCode);
        }
    }

    [Fact]
    public void Three_employees_have_left_and_nobody_joins_after_the_as_of_date()
    {
        var leavers = Employees.Where(e => !e.IsActive).ToList();

        leavers.Should().HaveCount(3);
        leavers.Should().OnlyContain(e => e.SeparationDate > e.JoiningDate && e.SeparationDate < AsOf);
        Employees.Should().OnlyContain(e => e.JoiningDate < AsOf);
        Employees.Where(e => e.EmploymentType == EmploymentType.Intern).Should().HaveCount(2);
    }

    [Fact]
    public void Every_department_is_headed_by_one_of_its_own_employees()
    {
        _data.DepartmentHeads.Keys.ToHashSet().SetEquals(_data.Departments).Should().BeTrue();
        _data.DepartmentHeads.Should().OnlyContain(pair => pair.Value.Department == pair.Key);
    }

    [Fact]
    public void The_same_date_and_seed_always_give_the_same_company()
    {
        var again = NordwinDemoData.Generate(AsOf);

        again.Employees.Select(Snapshot).Should().Equal(_data.Employees.Select(Snapshot));
    }

    private static string Snapshot(DemoEmployee e) =>
        $"{e.Employee.EmployeeCode}|{e.Employee.FullName}|{e.Employee.DateOfBirth}|{e.Employee.JoiningDate}|{e.Role}|{e.Employee.ReportingManager?.EmployeeCode}";
}
