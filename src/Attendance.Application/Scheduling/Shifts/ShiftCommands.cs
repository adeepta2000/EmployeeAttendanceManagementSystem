using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Scheduling.Shifts;

/// <summary>
/// Defines a shift (FR-2.1). <see cref="CrossesMidnight"/> is derived from the times; it may be sent, e.g.
/// from a "night shift" checkbox, but is then checked against them rather than stored as given.
/// </summary>
/// <param name="CrossesMidnight">Optional. If given it must be true exactly when <paramref name="EndTime"/> is earlier in the day than <paramref name="StartTime"/>.</param>
public sealed record CreateShiftCommand(
    string Code,
    string Name,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool? CrossesMidnight,
    int BreakMinutes,
    int GraceInMinutes,
    int GraceOutMinutes,
    int HalfDayThresholdMinutes,
    int FullDayMinutes,
    int PunchWindowBeforeMinutes,
    int PunchWindowAfterMinutes)
    : ICommand<Result<ShiftDto>>, IShiftFields;

/// <summary>
/// Replaces a shift's definition. It applies to every date processed from now on, including past dates
/// that are processed again; to change hours from a given date, define a new shift and roster it.
/// </summary>
public sealed record UpdateShiftCommand(
    Guid PublicId,
    string Code,
    string Name,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool? CrossesMidnight,
    int BreakMinutes,
    int GraceInMinutes,
    int GraceOutMinutes,
    int HalfDayThresholdMinutes,
    int FullDayMinutes,
    int PunchWindowBeforeMinutes,
    int PunchWindowAfterMinutes)
    : ICommand<Result<ShiftDto>>, IShiftFields;

/// <summary>Retires or restores a shift. Retiring is refused while it is an active employee's default shift.</summary>
public sealed record SetShiftActiveCommand(Guid PublicId, bool IsActive) : ICommand<Result<ShiftDto>>;

internal interface IShiftFields
{
    string Code { get; }

    string Name { get; }

    TimeOnly? StartTime { get; }

    TimeOnly? EndTime { get; }

    bool? CrossesMidnight { get; }

    int BreakMinutes { get; }

    int GraceInMinutes { get; }

    int GraceOutMinutes { get; }

    int HalfDayThresholdMinutes { get; }

    int FullDayMinutes { get; }

    int PunchWindowBeforeMinutes { get; }

    int PunchWindowAfterMinutes { get; }
}

internal static class ShiftRules
{
    public const int MaxGraceMinutes = 120;
    public const int MaxPunchWindowMinutes = 12 * 60;

    public static void AddShiftRules<T>(this AbstractValidator<T> validator)
        where T : IShiftFields
    {
        validator.RuleFor(s => s.Code).MustBeCode(Shift.CodeMaxLength);
        validator.RuleFor(s => s.Name).MustBeName(Shift.NameMaxLength);

        validator.RuleFor(s => s.StartTime).NotNull().Must(IsWholeMinute)
            .WithMessage("'{PropertyName}' must be a whole minute, e.g. 08:30.");
        validator.RuleFor(s => s.EndTime).NotNull().Must(IsWholeMinute)
            .WithMessage("'{PropertyName}' must be a whole minute, e.g. 17:30.");

        validator.RuleFor(s => s.BreakMinutes).GreaterThanOrEqualTo(0);
        validator.RuleFor(s => s.GraceInMinutes).InclusiveBetween(0, MaxGraceMinutes);
        validator.RuleFor(s => s.GraceOutMinutes).InclusiveBetween(0, MaxGraceMinutes);
        validator.RuleFor(s => s.FullDayMinutes).GreaterThan(0);
        validator.RuleFor(s => s.HalfDayThresholdMinutes).GreaterThan(0);
        validator.RuleFor(s => s.PunchWindowBeforeMinutes).InclusiveBetween(0, MaxPunchWindowMinutes);
        validator.RuleFor(s => s.PunchWindowAfterMinutes).InclusiveBetween(0, MaxPunchWindowMinutes);

        // The rules below compare fields, so they need both times.
        validator.When(s => s.StartTime.HasValue && s.EndTime.HasValue, () =>
        {
            validator.RuleFor(s => s.EndTime).NotEqual(s => s.StartTime)
                .WithMessage("A shift can't end at the time it starts.");

            validator.RuleFor(s => s.CrossesMidnight)
                .Must((s, crossesMidnight) => crossesMidnight == (s.EndTime < s.StartTime))
                .When(s => s.CrossesMidnight.HasValue)
                .WithMessage(s => s.EndTime < s.StartTime
                    ? $"A shift from {s.StartTime:HH:mm} to {s.EndTime:HH:mm} ends the next day, so it crosses midnight."
                    : $"A shift from {s.StartTime:HH:mm} to {s.EndTime:HH:mm} ends the same day, so it doesn't cross midnight.");

            validator.RuleFor(s => s.BreakMinutes)
                .LessThan(s => DurationMinutes(s))
                .WithMessage("The break must be shorter than the shift.");

            validator.RuleFor(s => s.FullDayMinutes)
                .LessThanOrEqualTo(s => DurationMinutes(s) - s.BreakMinutes)
                .When(s => s.BreakMinutes >= 0)
                .WithMessage("A full day can't need more minutes than the shift has without its break.");

            validator.RuleFor(s => s.HalfDayThresholdMinutes)
                .LessThanOrEqualTo(s => s.FullDayMinutes)
                .WithMessage("The half-day threshold can't be more than a full day.");

            validator.RuleFor(s => s.PunchWindowAfterMinutes)
                .Must((s, after) => s.PunchWindowBeforeMinutes + DurationMinutes(s) + after <= Shift.MinutesPerDay)
                .When(s => s.PunchWindowBeforeMinutes >= 0)
                .WithMessage("The punch window (before, the shift, and after) can't be longer than 24 hours, or one punch could count for two days.");
        });
    }

    /// <summary>Copies the fields onto a shift, normalized as stored. Validation must have passed.</summary>
    public static void Apply(this Shift shift, IShiftFields fields)
    {
        shift.Code = OrgCodes.Normalize(fields.Code);
        shift.Name = fields.Name.Trim();
        shift.StartTime = fields.StartTime!.Value;
        shift.EndTime = fields.EndTime!.Value;
        shift.BreakMinutes = fields.BreakMinutes;
        shift.GraceInMinutes = fields.GraceInMinutes;
        shift.GraceOutMinutes = fields.GraceOutMinutes;
        shift.HalfDayThresholdMinutes = fields.HalfDayThresholdMinutes;
        shift.FullDayMinutes = fields.FullDayMinutes;
        shift.PunchWindowBeforeMinutes = fields.PunchWindowBeforeMinutes;
        shift.PunchWindowAfterMinutes = fields.PunchWindowAfterMinutes;
    }

    public static Shift CreateShift(IShiftFields fields)
    {
        var shift = new Shift
        {
            Code = string.Empty,
            Name = string.Empty,
            StartTime = fields.StartTime!.Value,
            EndTime = fields.EndTime!.Value,
        };
        shift.Apply(fields);
        return shift;
    }

    private static bool IsWholeMinute(TimeOnly? time) => time is not { } t || t.Ticks % TimeSpan.TicksPerMinute == 0;

    private static int DurationMinutes(IShiftFields s) => (int)(s.EndTime!.Value - s.StartTime!.Value).TotalMinutes;
}

internal sealed class CreateShiftCommandValidator : AbstractValidator<CreateShiftCommand>
{
    public CreateShiftCommandValidator() => this.AddShiftRules();
}

internal sealed class UpdateShiftCommandValidator : AbstractValidator<UpdateShiftCommand>
{
    public UpdateShiftCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
        this.AddShiftRules();
    }
}

internal sealed class SetShiftActiveCommandValidator : AbstractValidator<SetShiftActiveCommand>
{
    public SetShiftActiveCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
    }
}
