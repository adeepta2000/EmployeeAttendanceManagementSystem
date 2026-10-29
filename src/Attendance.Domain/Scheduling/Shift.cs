using Attendance.Domain.Common;

namespace Attendance.Domain.Scheduling;

/// <summary>
/// A working shift (FR-2.1). Times are Asia/Dhaka wall-clock times. <see cref="CrossesMidnight"/> is derived
/// from them, never set: a shift that ends earlier in the day than it starts runs into the next day, and
/// the whole of it belongs to the date it starts on (FR-4.6, BR-12).
/// </summary>
public sealed class Shift : BaseEntity, IMasterData
{
    public const int CodeMaxLength = 50;
    public const int NameMaxLength = 50;
    public const int MinutesPerDay = 24 * 60;

    public Guid PublicId { get; private set; } = Guid.NewGuid();

    public required string Code { get; set; }

    public required string Name { get; set; }

    public required TimeOnly StartTime
    {
        get;
        set
        {
            field = value;
            CrossesMidnight = EndTime < value;
        }
    }

    public required TimeOnly EndTime
    {
        get;
        set
        {
            field = value;
            CrossesMidnight = value < StartTime;
        }
    }

    /// <summary>
    /// True when the shift ends on the calendar day after it starts. Kept in step with the times by their
    /// setters, and checked again by a database constraint.
    /// </summary>
    public bool CrossesMidnight { get; private set; }

    public int BreakMinutes { get; set; }

    /// <summary>How late an arrival may be before it counts as late-in (BR-03).</summary>
    public int GraceInMinutes { get; set; }

    /// <summary>How early a departure may be before it counts as early-out.</summary>
    public int GraceOutMinutes { get; set; }

    /// <summary>Worked minutes above zero but below this make the day a half day (BR-04).</summary>
    public int HalfDayThresholdMinutes { get; set; }

    /// <summary>Worked minutes that make a full day.</summary>
    public int FullDayMinutes { get; set; }

    /// <summary>How long before the start a punch is still attributed to this shift.</summary>
    public int PunchWindowBeforeMinutes { get; set; }

    /// <summary>How long after the end a punch is still attributed to this shift.</summary>
    public int PunchWindowAfterMinutes { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Scheduled length from start to end, breaks included. Zero only for the invalid start = end.</summary>
    // TimeOnly subtraction wraps past midnight: 06:00 - 22:00 is 8 hours.
    public int DurationMinutes => (int)(EndTime - StartTime).TotalMinutes;

    /// <summary>
    /// The shift placed on <paramref name="shiftDate"/>, the date it starts on. A night shift's end and
    /// punch window run into the next day but still belong to <paramref name="shiftDate"/> (BR-12).
    /// </summary>
    public ShiftSchedule GetSchedule(DateOnly shiftDate)
    {
        // Asia/Dhaka has had no daylight saving since 2009, so wall-clock minutes add up exactly.
        var start = shiftDate.ToDateTime(StartTime);
        var end = start.AddMinutes(DurationMinutes);
        return new ShiftSchedule(
            shiftDate,
            start,
            end,
            start.AddMinutes(-PunchWindowBeforeMinutes),
            end.AddMinutes(PunchWindowAfterMinutes));
    }
}

/// <summary>A shift on a particular date, as Asia/Dhaka local date-times (<see cref="DateTimeKind.Unspecified"/>).</summary>
/// <param name="ShiftDate">The date the shift belongs to: the date it starts on (BR-12).</param>
/// <param name="PunchWindowStart">The earliest punch attributed to this shift.</param>
/// <param name="PunchWindowEnd">The latest punch attributed to this shift.</param>
public sealed record ShiftSchedule(
    DateOnly ShiftDate,
    DateTime Start,
    DateTime End,
    DateTime PunchWindowStart,
    DateTime PunchWindowEnd);
