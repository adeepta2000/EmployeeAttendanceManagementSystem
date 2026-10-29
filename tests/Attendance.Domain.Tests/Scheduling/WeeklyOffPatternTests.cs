using Attendance.Domain.Scheduling;
using FluentAssertions;

namespace Attendance.Domain.Tests.Scheduling;

public sealed class WeeklyOffPatternTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    [InlineData(14, 2)]
    [InlineData(15, 3)]
    [InlineData(22, 4)]
    [InlineData(28, 4)]
    [InlineData(29, 5)]
    [InlineData(31, 5)]
    public void Week_of_month_is_which_occurrence_of_its_weekday_the_date_is(int day, int expected) =>
        WeeklyOffPattern.WeekOfMonth(new DateOnly(2026, 8, day)).Should().Be(expected);

    [Fact]
    public void Valid_patterns_have_no_problem()
    {
        WeeklyOffPattern.FindProblem([new WeeklyOffDay(DayOfWeek.Friday)]).Should().BeNull();
        WeeklyOffPattern.FindProblem(
            [new WeeklyOffDay(DayOfWeek.Friday), new WeeklyOffDay(DayOfWeek.Saturday, 2), new WeeklyOffDay(DayOfWeek.Saturday, 4)])
            .Should().BeNull();
    }

    public static TheoryData<WeeklyOffDay[]> InvalidPatterns => new()
    {
        Array.Empty<WeeklyOffDay>(),
        new[] { new WeeklyOffDay(DayOfWeek.Friday), new WeeklyOffDay(DayOfWeek.Friday) },
        new[] { new WeeklyOffDay(DayOfWeek.Saturday, 2), new WeeklyOffDay(DayOfWeek.Saturday, 2) },
        new[] { new WeeklyOffDay(DayOfWeek.Saturday, 0) },
        new[] { new WeeklyOffDay(DayOfWeek.Saturday, 6) },
        new[] { new WeeklyOffDay((DayOfWeek)7) },
        new[] { new WeeklyOffDay(DayOfWeek.Friday), new WeeklyOffDay(DayOfWeek.Friday, 2) },
        Enum.GetValues<DayOfWeek>().Select(d => new WeeklyOffDay(d)).ToArray(),
    };

    [Theory]
    [MemberData(nameof(InvalidPatterns))]
    public void Invalid_patterns_are_refused(WeeklyOffDay[] days)
    {
        WeeklyOffPattern.FindProblem(days).Should().NotBeNull();

        var act = () => new WeeklyOffPattern("Invalid", days);
        act.Should().Throw<ArgumentException>().WithParameterName("days");
    }

    [Fact]
    public void A_pattern_keeps_the_days_it_was_created_with()
    {
        var pattern = new WeeklyOffPattern("Friday + alternate Saturdays",
            [new WeeklyOffDay(DayOfWeek.Friday), new WeeklyOffDay(DayOfWeek.Saturday, 2), new WeeklyOffDay(DayOfWeek.Saturday, 4)]);

        pattern.Days.Select(d => (d.DayOfWeek, d.WeekOfMonth)).Should().BeEquivalentTo(
            new (DayOfWeek, byte?)[] { (DayOfWeek.Friday, null), (DayOfWeek.Saturday, 2), (DayOfWeek.Saturday, 4) });
    }
}
