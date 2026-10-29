using FluentValidation;

namespace Attendance.Application.Scheduling;

internal static class ScheduleLimits
{
    /// <summary>The longest date range a roster list, bulk assignment or calendar request may cover: three months.</summary>
    public const int MaxRangeDays = 92;

    /// <summary>Bounds for every scheduling date, to catch typos such as 0026 for 2026.</summary>
    public static readonly DateOnly EarliestDate = new(2000, 1, 1);

    public static readonly DateOnly LatestDate = new(2099, 12, 31);

    /// <summary>Days from <paramref name="from"/> to <paramref name="to"/>, both included.</summary>
    public static int DayCount(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber + 1;
}

internal static class ScheduleValidationRules
{
    public static IRuleBuilderOptions<T, DateOnly> MustBeScheduleDate<T>(this IRuleBuilder<T, DateOnly> rule) =>
        rule.InclusiveBetween(ScheduleLimits.EarliestDate, ScheduleLimits.LatestDate)
            .WithMessage($"'{{PropertyName}}' must be between {ScheduleLimits.EarliestDate:yyyy-MM-dd} and {ScheduleLimits.LatestDate:yyyy-MM-dd}.");

    /// <summary>The end is on or after the start, and the range is at most <see cref="ScheduleLimits.MaxRangeDays"/> days.</summary>
    public static IRuleBuilderOptions<T, DateOnly> MustEndWithinRange<T>(this IRuleBuilder<T, DateOnly> rule, Func<T, DateOnly> from) =>
        rule.Must((request, to) => to >= from(request) && ScheduleLimits.DayCount(from(request), to) <= ScheduleLimits.MaxRangeDays)
            .WithMessage($"'{{PropertyName}}' must be on or after the start date and at most {ScheduleLimits.MaxRangeDays} days from it, both included.");
}
