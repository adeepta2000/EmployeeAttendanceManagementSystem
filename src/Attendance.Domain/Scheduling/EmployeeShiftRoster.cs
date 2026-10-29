using Attendance.Domain.Common;
using Attendance.Domain.Organization;
using Attendance.Domain.Processing;

namespace Attendance.Domain.Scheduling;

/// <summary>
/// The plan for one employee on one date (FR-2.3): a shift that replaces their default shift, and
/// optionally a weekly-off override. There is one row per employee and date at most; a removed entry is
/// soft-deleted and restored if the date is rostered again.
/// </summary>
/// <remarks>
/// Every change to the plan raises <see cref="AttendanceRecalculationRequested"/>, so a date that was
/// already processed is processed again (FR-2.5).
/// </remarks>
public sealed class EmployeeShiftRoster : BaseEntity, IHasPublicId, ISoftDeletable, IHasDomainEvents
{
    public const int RemarksMaxLength = 300;

    private readonly List<IDomainEvent> _domainEvents = [];

    // For EF Core and Create.
    private EmployeeShiftRoster()
    {
    }

    public Guid PublicId { get; private set; } = Guid.NewGuid();

    public long EmployeeId { get; private set; }

    public Employee Employee { get; private set; } = null!;

    public DateOnly RosterDate { get; private set; }

    public long ShiftId { get; private set; }

    public Shift Shift { get; private set; } = null!;

    /// <summary>
    /// Null: the employee's weekly-off pattern decides whether the date is a weekly off. True: it is a
    /// weekly off whatever the pattern says. False: it is a working day even if the pattern makes it a
    /// weekly off (a swapped day off).
    /// </summary>
    public bool? IsWeeklyOffOverride { get; private set; }

    public string? Remarks { get; private set; }

    public bool IsDeleted { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>Rosters <paramref name="shift"/> for the employee on <paramref name="rosterDate"/>.</summary>
    public static EmployeeShiftRoster Create(
        long employeeId,
        DateOnly rosterDate,
        Shift shift,
        bool? isWeeklyOffOverride,
        string? remarks)
    {
        var roster = new EmployeeShiftRoster { EmployeeId = employeeId, RosterDate = rosterDate };
        roster.Apply(shift, isWeeklyOffOverride, remarks);
        roster.RequestRecalculation();
        return roster;
    }

    /// <summary>Changes the entry. Only a change of shift or weekly-off override requests a recalculation; remarks don't.</summary>
    /// <returns>True if the plan for the date changed.</returns>
    /// <exception cref="InvalidOperationException">The entry was removed; use <see cref="Restore"/>.</exception>
    public bool Update(Shift shift, bool? isWeeklyOffOverride, string? remarks)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A removed roster entry can't be updated; restore it instead.");
        }

        var planChanged = !IsShift(shift) || IsWeeklyOffOverride != isWeeklyOffOverride;
        Apply(shift, isWeeklyOffOverride, remarks);
        if (planChanged)
        {
            RequestRecalculation();
        }

        return planChanged;
    }

    /// <summary>Brings back a removed entry with new values, when its employee and date are rostered again.</summary>
    /// <exception cref="InvalidOperationException">The entry wasn't removed.</exception>
    public void Restore(Shift shift, bool? isWeeklyOffOverride, string? remarks)
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("Only a removed roster entry can be restored.");
        }

        IsDeleted = false;
        Apply(shift, isWeeklyOffOverride, remarks);
        RequestRecalculation();
    }

    /// <summary>Removes the entry (a soft delete): the date falls back to the default shift and weekly-off pattern.</summary>
    public void Remove()
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        RequestRecalculation();
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void Apply(Shift shift, bool? isWeeklyOffOverride, string? remarks)
    {
        Shift = shift;
        ShiftId = shift.Id;
        IsWeeklyOffOverride = isWeeklyOffOverride;
        Remarks = remarks;
    }

    // PublicId, not Id: a shift that isn't saved yet has no Id.
    private bool IsShift(Shift shift) => Shift is not null && Shift.PublicId == shift.PublicId;

    private void RequestRecalculation() =>
        _domainEvents.Add(new AttendanceRecalculationRequested([EmployeeId], RosterDate, RosterDate, RecalculationReason.RosterChanged));
}
