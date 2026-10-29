using Attendance.Domain.Processing;
using Attendance.Domain.Scheduling;
using FluentAssertions;

namespace Attendance.Domain.Tests.Scheduling;

/// <summary>FR-2.5: every change to a roster entry asks for its date to be processed again.</summary>
public sealed class EmployeeShiftRosterTests
{
    private const long EmployeeId = 42;
    private static readonly DateOnly Date = new(2026, 11, 2);
    private static readonly Shift General = NewShift("GEN");
    private static readonly Shift Night = NewShift("NIGHT");

    [Fact]
    public void Creating_an_entry_requests_a_recalculation_of_its_date()
    {
        var roster = EmployeeShiftRoster.Create(EmployeeId, Date, General, isWeeklyOffOverride: null, remarks: null);

        roster.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AttendanceRecalculationRequested>()
            .Which.Should().Match<AttendanceRecalculationRequested>(e =>
                e.EmployeeIds.SequenceEqual(new[] { EmployeeId }) && e.From == Date && e.To == Date
                && e.Reason == RecalculationReason.RosterChanged);
    }

    [Fact]
    public void Changing_the_shift_or_the_weekly_off_override_requests_a_recalculation()
    {
        var roster = CreateWithoutEvents();

        roster.Update(Night, isWeeklyOffOverride: null, remarks: null).Should().BeTrue();
        roster.DomainEvents.Should().ContainSingle();
        roster.ClearDomainEvents();

        roster.Update(Night, isWeeklyOffOverride: true, remarks: null).Should().BeTrue();
        roster.DomainEvents.Should().ContainSingle();
        roster.Shift.Should().BeSameAs(Night);
        roster.IsWeeklyOffOverride.Should().BeTrue();
    }

    [Fact]
    public void Changing_only_the_remarks_requests_nothing()
    {
        var roster = CreateWithoutEvents();

        roster.Update(General, isWeeklyOffOverride: null, remarks: "Covering for a colleague").Should().BeFalse();

        roster.DomainEvents.Should().BeEmpty();
        roster.Remarks.Should().Be("Covering for a colleague");
    }

    [Fact]
    public void Removing_an_entry_requests_a_recalculation_once()
    {
        var roster = CreateWithoutEvents();

        roster.Remove();
        roster.Remove();

        roster.IsDeleted.Should().BeTrue();
        roster.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void A_removed_entry_is_restored_rather_than_updated()
    {
        var roster = CreateWithoutEvents();
        roster.Remove();
        roster.ClearDomainEvents();

        var update = () => roster.Update(Night, null, null);
        update.Should().Throw<InvalidOperationException>();

        roster.Restore(Night, isWeeklyOffOverride: false, remarks: null);
        roster.IsDeleted.Should().BeFalse();
        roster.Shift.Should().BeSameAs(Night);
        roster.DomainEvents.Should().ContainSingle();
    }

    private static EmployeeShiftRoster CreateWithoutEvents()
    {
        var roster = EmployeeShiftRoster.Create(EmployeeId, Date, General, isWeeklyOffOverride: null, remarks: null);
        roster.ClearDomainEvents();
        return roster;
    }

    private static Shift NewShift(string code) => new()
    {
        Code = code,
        Name = code,
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(18, 0),
    };
}
