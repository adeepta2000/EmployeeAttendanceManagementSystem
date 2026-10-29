using Attendance.Domain.Scheduling;
using FluentAssertions;

namespace Attendance.Domain.Tests.Scheduling;

public sealed class HolidayTests
{
    private const long Dhaka = 10;
    private const long Chittagong = 20;

    [Fact]
    public void The_year_follows_the_date()
    {
        var holiday = new Holiday { Name = "New Year", HolidayDate = new DateOnly(2026, 12, 31) };

        holiday.HolidayDate = new DateOnly(2027, 1, 1);

        holiday.Year.Should().Be(2027);
    }

    [Fact]
    public void A_holiday_without_locations_applies_everywhere_and_one_with_locations_only_there()
    {
        var holiday = new Holiday { Name = "Dhaka City Day", HolidayDate = new DateOnly(2026, 11, 3) };
        holiday.AppliesTo(Dhaka).Should().BeTrue();
        holiday.AppliesTo(Chittagong).Should().BeTrue();

        holiday.SetLocations([Dhaka]);

        holiday.AppliesTo(Dhaka).Should().BeTrue();
        holiday.AppliesTo(Chittagong).Should().BeFalse();
    }

    [Fact]
    public void Unlisting_a_location_soft_deletes_its_link_and_relisting_restores_the_same_link()
    {
        var holiday = new Holiday { Name = "Regional Day", HolidayDate = new DateOnly(2026, 11, 3) };
        holiday.SetLocations([Dhaka, Chittagong]);
        var dhakaLink = holiday.Locations.Single(l => l.WorkLocationId == Dhaka);

        holiday.SetLocations([Chittagong]);
        dhakaLink.IsDeleted.Should().BeTrue();
        holiday.WorkLocationIds.Should().Equal(Chittagong);

        holiday.SetLocations([Dhaka, Chittagong]);
        holiday.Locations.Should().HaveCount(2, "the removed link came back instead of a second one being added");
        dhakaLink.IsDeleted.Should().BeFalse();

        holiday.SetLocations([]);
        holiday.WorkLocationIds.Should().BeEmpty();
        holiday.AppliesTo(Dhaka).Should().BeTrue("with every link removed it applies everywhere again");
    }
}
