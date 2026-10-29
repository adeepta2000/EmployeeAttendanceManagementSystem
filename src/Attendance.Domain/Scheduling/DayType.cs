namespace Attendance.Domain.Scheduling;

/// <summary>
/// What kind of day a date is for one employee (spec §3.2: AttendanceDaily.DayType, OvertimeRules.DayType).
/// It drives the attendance status and the OT multiplier (FR-5.1, FR-7.2). Persisted as TINYINT, so never
/// renumber existing members. Zero is deliberately unused.
/// </summary>
public enum DayType : byte
{
    Working = 1,
    WeeklyOff = 2,
    Holiday = 3,
}
