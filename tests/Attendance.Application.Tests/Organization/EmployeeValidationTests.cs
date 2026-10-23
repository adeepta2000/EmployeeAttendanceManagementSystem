using Attendance.Application.Organization;
using Attendance.Application.Organization.Employees;
using Attendance.Domain.Organization;
using FluentAssertions;

namespace Attendance.Application.Tests.Organization;

public sealed class EmployeeValidationTests
{
    private static readonly DateOnly Joining = new(2024, 3, 1);

    private readonly CreateEmployeeCommandValidator _validator = new();

    [Fact]
    public void A_valid_command_passes() =>
        _validator.Validate(Command()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void The_employee_must_be_at_least_14_on_the_joining_date(int daysShort, bool valid)
    {
        var command = Command() with { DateOfBirth = Joining.AddYears(-14).AddDays(daysShort) };

        _validator.Validate(command).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("nw-001", true)]
    [InlineData("NW/2024/7", true)]
    [InlineData("-NW", false)]
    [InlineData("NW 001", false)]
    [InlineData("NW-001-ABCDEFGHIJKLMNOPQRSTUVWXYZ", false)]
    public void Employee_codes_are_short_and_plain(string code, bool valid) =>
        _validator.Validate(Command() with { EmployeeCode = code }).IsValid.Should().Be(valid);

    [Fact]
    public void Confirmation_cannot_precede_joining() =>
        _validator.Validate(Command() with { ConfirmationDate = Joining.AddDays(-1) })
            .Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(CreateEmployeeCommand.ConfirmationDate));

    [Fact]
    public void Missing_values_and_undefined_enum_members_are_caught()
    {
        var command = Command() with
        {
            DateOfBirth = default,
            JoiningDate = default,
            Gender = (Gender)0,
            EmploymentType = (EmploymentType)9,
            WorkLocationId = Guid.Empty,
            ReportingManagerId = Guid.Empty,
        };

        _validator.Validate(command).Errors.Select(e => e.PropertyName).Distinct().Should().BeEquivalentTo(
        [
            nameof(CreateEmployeeCommand.DateOfBirth), nameof(CreateEmployeeCommand.JoiningDate), nameof(CreateEmployeeCommand.Gender),
            nameof(CreateEmployeeCommand.EmploymentType), nameof(CreateEmployeeCommand.WorkLocationId),
            nameof(CreateEmployeeCommand.ReportingManagerId),
        ]);
    }

    [Fact]
    public void Personal_data_stays_out_of_ToString()
    {
        var command = Command();
        var dto = new EmployeeDto(
            Guid.NewGuid(), "NW-001", "Rafiq", "Islam", "Rafiq Islam", command.DateOfBirth, Gender.Male, "rafiq@nordwin.example", null,
            new OrgUnitRef(Guid.NewGuid(), "SEW", "Sewing"), new OrgUnitRef(Guid.NewGuid(), "OPR", "Operator"),
            new OrgUnitRef(Guid.NewGuid(), "DHK", "Dhaka"), EmploymentType.Permanent, null, Joining, null, null, true, false);

        foreach (var text in new[] { command.ToString(), (command with { }).ToString(), dto.ToString() })
        {
            text.Should().NotContain("Rafiq").And.NotContain("Islam").And.NotContain("@");
        }
    }

    private static CreateEmployeeCommand Command() => new(
        "NW-001", "Rafiq", "Islam", new DateOnly(1990, 5, 17), Gender.Male, "rafiq.islam@nordwin.example", "+880 1700-000000",
        Joining, Joining.AddMonths(6), EmploymentType.Permanent, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);
}
