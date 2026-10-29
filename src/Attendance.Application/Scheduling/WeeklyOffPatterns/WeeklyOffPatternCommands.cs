using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Scheduling.WeeklyOffPatterns;

/// <summary>
/// Defines a weekly-off pattern (FR-2.2): "every Friday" is one day with no week of month; "Friday and
/// alternate Saturdays" adds Saturday for weeks 2 and 4. The days can't be changed afterwards.
/// </summary>
public sealed record CreateWeeklyOffPatternCommand(string Name, IReadOnlyList<WeeklyOffDay> Days)
    : ICommand<Result<WeeklyOffPatternDto>>;

/// <summary>
/// Renames a pattern. Its days are fixed, so that past weekly offs never change: to use other days,
/// create a new pattern, move the employees to it and retire this one.
/// </summary>
public sealed record RenameWeeklyOffPatternCommand(Guid PublicId, string Name) : ICommand<Result<WeeklyOffPatternDto>>;

/// <summary>Retires or restores a pattern. Retiring is refused while active employees follow it.</summary>
public sealed record SetWeeklyOffPatternActiveCommand(Guid PublicId, bool IsActive) : ICommand<Result<WeeklyOffPatternDto>>;

internal sealed class CreateWeeklyOffPatternCommandValidator : AbstractValidator<CreateWeeklyOffPatternCommand>
{
    // Seven weekdays, each every week or in up to five particular weeks, is the most a valid pattern can list.
    private const int MaxDays = 7 * WeeklyOffPattern.MaxWeekOfMonth;

    public CreateWeeklyOffPatternCommandValidator()
    {
        RuleFor(c => c.Name).MustBeName(WeeklyOffPattern.NameMaxLength);
        RuleFor(c => c.Days).NotEmpty().Must(days => days.Count <= MaxDays)
            .WithMessage($"'{{PropertyName}}' can list at most {MaxDays} days.");
        RuleForEach(c => c.Days).ChildRules(day =>
        {
            day.RuleFor(d => d.DayOfWeek).IsInEnum();
            day.RuleFor(d => d.WeekOfMonth).InclusiveBetween(1, WeeklyOffPattern.MaxWeekOfMonth);
        });
        RuleFor(c => c.Days).Custom((days, context) =>
        {
            if (days is { Count: > 0 } && WeeklyOffPattern.FindProblem(days) is { } problem)
            {
                context.AddFailure(problem);
            }
        });
    }
}

internal sealed class RenameWeeklyOffPatternCommandValidator : AbstractValidator<RenameWeeklyOffPatternCommand>
{
    public RenameWeeklyOffPatternCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
        RuleFor(c => c.Name).MustBeName(WeeklyOffPattern.NameMaxLength);
    }
}

internal sealed class SetWeeklyOffPatternActiveCommandValidator : AbstractValidator<SetWeeklyOffPatternActiveCommand>
{
    public SetWeeklyOffPatternActiveCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
    }
}
