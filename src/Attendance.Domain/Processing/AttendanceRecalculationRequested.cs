using Attendance.Domain.Common;

namespace Attendance.Domain.Processing;

/// <summary>
/// An input of attendance processing changed for these employees between <see cref="From"/> and
/// <see cref="To"/> (inclusive), so any of those dates already processed must be processed again (FR-2.5,
/// spec §2.7). Raising it for a date that was never processed is harmless: the handler skips it, and
/// processing is idempotent (FR-4.5).
/// </summary>
/// <param name="EmployeeIds">Internal employee IDs. The event never leaves the process.</param>
public sealed record AttendanceRecalculationRequested(
    IReadOnlyList<long> EmployeeIds,
    DateOnly From,
    DateOnly To,
    RecalculationReason Reason) : IDomainEvent;

/// <summary>Why a recalculation was requested. Leave and regularization approvals join in later phases.</summary>
public enum RecalculationReason
{
    RosterChanged = 1,
}
