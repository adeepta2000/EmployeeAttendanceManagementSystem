using System.Reflection;
using Attendance.Domain.Identity;
using FluentAssertions;

namespace Attendance.Domain.Tests.Identity;

public sealed class PermissionsTests
{
    [Fact]
    public void Every_permission_constant_is_in_the_catalog_exactly_once()
    {
        var constants = typeof(Permissions).GetNestedTypes()
            .Where(t => t != typeof(Permissions.Modules))
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Permissions.All.Select(p => p.Code).Should().BeEquivalentTo(constants)
            .And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void The_codes_FR_8_2_names_are_in_the_catalog()
    {
        Permissions.All.Select(p => p.Code).Should().Contain(
            ["attendance.process", "leave.approve", "report.export", "employee.write"]);
    }

    [Fact]
    public void Every_entry_fits_the_Permissions_table()
    {
        Permissions.All.Should().AllSatisfy(p =>
        {
            p.Code.Should().MatchRegex("^[a-z]+\\.[a-z]+$", "codes are <resource>.<action>");
            p.Code.Length.Should().BeLessThanOrEqualTo(Permission.CodeMaxLength);
            p.Module.Length.Should().BeLessThanOrEqualTo(Permission.ModuleMaxLength);
            p.Description.Length.Should().BeLessThanOrEqualTo(Permission.DescriptionMaxLength);
        });
    }
}
