using Attendance.Application.Common.Security;
using Attendance.Application.Scheduling.Rosters;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Paging;

namespace Attendance.Application.Scheduling.Abstractions;

// Shifts are master data and use IMasterDataRepository<Shift>.

public interface IWeeklyOffPatternRepository
{
    /// <summary>Finds a pattern by PublicId, active or not, with its days. Tracked.</summary>
    Task<WeeklyOffPattern?> FindAsync(Guid publicId, CancellationToken cancellationToken);

    /// <summary>Every pattern, or only active or retired ones, with their days, ordered by name. Not tracked.</summary>
    Task<IReadOnlyList<WeeklyOffPattern>> ListAsync(bool? isActive, CancellationToken cancellationToken);

    /// <summary>True when any pattern, active or not, already has the name (compared as the database collation does).</summary>
    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken);

    void Add(WeeklyOffPattern pattern);
}

/// <summary>
/// Roster entries (FR-2.3, FR-2.4). Every lookup by PublicId takes the caller's scope (FR-8.3) and applies it
/// to the entry's employee, so an entry of someone outside the scope reads as not found.
/// </summary>
public interface IRosterRepository
{
    /// <summary>A live entry inside <paramref name="scope"/>, with its employee and shift loaded. Tracked.</summary>
    Task<EmployeeShiftRoster?> FindAsync(Guid publicId, EmployeeScope scope, CancellationToken cancellationToken);

    /// <summary>The employee's entry for the date, removed or not, with its shift loaded. Tracked.</summary>
    Task<EmployeeShiftRoster?> FindForDateAsync(long employeeId, DateOnly rosterDate, CancellationToken cancellationToken);

    /// <summary>One page of the live entries inside <paramref name="scope"/> that match, with employee and shift. Not tracked.</summary>
    Task<PagedResult<EmployeeShiftRoster>> ListAsync(ListRosterEntriesQuery query, EmployeeScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// Rosters a shift for the active employees of a department inside <paramref name="scope"/>, over a date
    /// range, in a fixed number of set-based statements however many employee-days that is (FR-2.4). Each
    /// employee-date gets exactly one live entry: missing ones are inserted, differing or removed ones
    /// overwritten. Dates before an employee joined are skipped.
    /// </summary>
    Task<RosterAssignmentOutcome> AssignToDepartmentAsync(
        long departmentId,
        RosterAssignment assignment,
        EmployeeScope scope,
        CancellationToken cancellationToken);

    /// <summary>
    /// The same set-based assignment for employees the caller has already resolved inside its scope (a
    /// selection on the team roster). Employees who join after the range are left out of the count.
    /// </summary>
    Task<RosterAssignmentOutcome> AssignToEmployeesAsync(
        IReadOnlyCollection<long> employeeIds,
        RosterAssignment assignment,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes (soft-deletes) the live entries of these employees from <paramref name="from"/> to
    /// <paramref name="to"/> inclusive, in one statement; the dates fall back to the default shift and pattern.
    /// </summary>
    Task<RosterAssignmentOutcome> RemoveAsync(
        IReadOnlyCollection<long> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    void Add(EmployeeShiftRoster roster);
}

/// <param name="ShiftId">Internal ID of the shift.</param>
public sealed record RosterAssignment(
    DateOnly FromDate,
    DateOnly ToDate,
    long ShiftId,
    bool? IsWeeklyOffOverride,
    string? Remarks);

/// <param name="EmployeeCount">Employees the assignment covered.</param>
/// <param name="Created">Entries inserted; always 0 for a removal.</param>
/// <param name="Updated">Entries changed, restored or (for a removal) removed.</param>
/// <param name="ChangedEmployeeIds">Employees with at least one inserted or changed entry.</param>
/// <param name="FirstChangedDate">Earliest date with an inserted or changed entry; null when nothing changed.</param>
/// <param name="LastChangedDate">Latest date with an inserted or changed entry; null when nothing changed.</param>
public sealed record RosterAssignmentOutcome(
    int EmployeeCount,
    int Created,
    int Updated,
    IReadOnlyList<long> ChangedEmployeeIds,
    DateOnly? FirstChangedDate,
    DateOnly? LastChangedDate);

public interface IHolidayRepository
{
    /// <summary>
    /// Finds a holiday by PublicId with all its location links, removed ones included, as
    /// <see cref="Holiday.SetLocations"/> needs, and their work locations. Tracked.
    /// </summary>
    Task<Holiday?> FindAsync(Guid publicId, CancellationToken cancellationToken);

    /// <summary>
    /// The holidays of a year, by date, with their live location links. With <paramref name="workLocationId"/>,
    /// only those that apply there: the ones limited to it and the ones that apply everywhere. Not tracked.
    /// </summary>
    Task<IReadOnlyList<Holiday>> ListAsync(int year, long? workLocationId, CancellationToken cancellationToken);

    /// <summary>True when another live holiday has the same date and name.</summary>
    Task<bool> ExistsAsync(DateOnly holidayDate, string name, long? exceptId, CancellationToken cancellationToken);

    void Add(Holiday holiday);
}

/// <summary>Loads what <see cref="CalendarService"/> needs, in a few queries whatever the size of the request.</summary>
public interface ICalendarRepository
{
    /// <summary>
    /// The calendar of these employees from <paramref name="from"/> to <paramref name="to"/> inclusive.
    /// Employees that don't exist are left out.
    /// </summary>
    Task<CalendarData> LoadAsync(IReadOnlyCollection<long> employeeIds, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
