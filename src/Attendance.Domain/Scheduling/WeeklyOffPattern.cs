using Attendance.Domain.Common;

namespace Attendance.Domain.Scheduling;

/// <summary>
/// Which days of the week are weekly offs (FR-2.2), such as every Friday, or every Friday plus the 2nd and
/// 4th Saturday ("alternate Saturdays").
/// </summary>
/// <remarks>
/// The days are fixed when the pattern is created. Editing them would silently change which past dates
/// were weekly offs, and would mean deleting rows. To change a pattern, create a new one, move the
/// employees to it and retire the old one.
/// </remarks>
public sealed class WeeklyOffPattern : BaseEntity, IHasPublicId
{
    public const int NameMaxLength = 100;

    /// <summary>A month has at most five of any weekday.</summary>
    public const int MaxWeekOfMonth = 5;

    private readonly List<WeeklyOffPatternDay> _days = [];

    /// <exception cref="ArgumentException">The days are not a valid pattern (see <see cref="FindProblem"/>).</exception>
    public WeeklyOffPattern(string name, IReadOnlyCollection<WeeklyOffDay> days)
    {
        if (FindProblem(days) is { } problem)
        {
            throw new ArgumentException(problem, nameof(days));
        }

        Name = name;
        _days.AddRange(days.Select(d => new WeeklyOffPatternDay(d.DayOfWeek, (byte?)d.WeekOfMonth)));
    }

    // For EF Core.
    private WeeklyOffPattern() => Name = null!;

    public Guid PublicId { get; private set; } = Guid.NewGuid();

    public string Name { get; set; }

    public bool IsActive { get; set; } = true;

    public IReadOnlyList<WeeklyOffPatternDay> Days => _days;

    /// <summary>
    /// Which occurrence of its weekday a date is within its month: 1 for days 1–7, 2 for 8–14, up to 5 for
    /// days 29–31. The 2nd Saturday of a month is the Saturday with week-of-month 2.
    /// </summary>
    public static int WeekOfMonth(DateOnly date) => ((date.Day - 1) / 7) + 1;

    /// <summary>
    /// Why <paramref name="days"/> can't form a pattern, or null if they can: there must be at least one
    /// day, no day twice, a week of month from 1 to <see cref="MaxWeekOfMonth"/>, no weekday both every
    /// week and in particular weeks, and at least one working day every week.
    /// </summary>
    public static string? FindProblem(IReadOnlyCollection<WeeklyOffDay> days)
    {
        if (days.Count == 0)
        {
            return "A weekly-off pattern needs at least one day.";
        }

        if (days.Any(d => !Enum.IsDefined(d.DayOfWeek)))
        {
            return "Every day must be a day of the week.";
        }

        if (days.Any(d => d.WeekOfMonth is < 1 or > MaxWeekOfMonth))
        {
            return $"The week of the month must be from 1 to {MaxWeekOfMonth}, or empty for every week.";
        }

        if (days.Distinct().Count() != days.Count)
        {
            return "A day is listed more than once.";
        }

        var everyWeek = days.Where(d => d.WeekOfMonth is null).Select(d => d.DayOfWeek).ToHashSet();
        if (days.Any(d => d.WeekOfMonth is not null && everyWeek.Contains(d.DayOfWeek)))
        {
            return "A day that is off every week can't also be listed for particular weeks.";
        }

        if (everyWeek.Count == 7)
        {
            return "Every day of the week can't be a weekly off.";
        }

        return null;
    }

    public bool IsWeeklyOff(DateOnly date)
    {
        var weekOfMonth = WeekOfMonth(date);
        return _days.Any(d => d.DayOfWeek == date.DayOfWeek && (d.WeekOfMonth is null || d.WeekOfMonth == weekOfMonth));
    }
}

/// <summary>One day of a <see cref="WeeklyOffPattern"/>. Created with the pattern and never changed.</summary>
public sealed class WeeklyOffPatternDay : BaseEntity
{
    internal WeeklyOffPatternDay(DayOfWeek dayOfWeek, byte? weekOfMonth)
    {
        DayOfWeek = dayOfWeek;
        WeekOfMonth = weekOfMonth;
    }

    public long PatternId { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }

    /// <summary>
    /// Which occurrence of <see cref="DayOfWeek"/> in the month (see <see cref="WeeklyOffPattern.WeekOfMonth"/>),
    /// or null for every week.
    /// </summary>
    public byte? WeekOfMonth { get; private set; }
}

/// <summary>A day of a weekly-off pattern, as given when creating one.</summary>
/// <param name="WeekOfMonth">1–5 for that occurrence in the month only; null for every week.</param>
public readonly record struct WeeklyOffDay(DayOfWeek DayOfWeek, int? WeekOfMonth = null);
