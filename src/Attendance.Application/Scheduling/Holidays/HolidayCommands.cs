using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Scheduling.Holidays;

/// <summary>
/// Adds a holiday to the calendar (FR-7.1). With no work locations it applies everywhere; otherwise only
/// at the locations listed, which must be active.
/// </summary>
/// <param name="WorkLocationIds">PublicIds of the work locations the holiday is limited to; empty or null for everywhere.</param>
public sealed record CreateHolidayCommand(
    string Name,
    DateOnly HolidayDate,
    bool IsOptional,
    string? Description,
    IReadOnlyList<Guid>? WorkLocationIds)
    : ICommand<Result<HolidayDto>>, IHolidayFields;

/// <summary>
/// Replaces a holiday's details, e.g. when a moon-sighted holiday moves by a day. A location it is
/// already limited to may stay listed after that location retires; a newly listed one must be active.
/// </summary>
public sealed record UpdateHolidayCommand(
    Guid PublicId,
    string Name,
    DateOnly HolidayDate,
    bool IsOptional,
    string? Description,
    IReadOnlyList<Guid>? WorkLocationIds)
    : ICommand<Result<HolidayDto>>, IHolidayFields;

/// <summary>Takes a holiday off the calendar. It is soft-deleted, never erased.</summary>
public sealed record DeleteHolidayCommand(Guid PublicId) : ICommand<Result>;

internal interface IHolidayFields
{
    string Name { get; }

    DateOnly HolidayDate { get; }

    bool IsOptional { get; }

    string? Description { get; }

    IReadOnlyList<Guid>? WorkLocationIds { get; }
}

internal static class HolidayRules
{
    public const int MaxWorkLocations = 50;

    public static void AddHolidayRules<T>(this AbstractValidator<T> validator)
        where T : IHolidayFields
    {
        validator.RuleFor(h => h.Name).MustBeName(Holiday.NameMaxLength);
        validator.RuleFor(h => h.HolidayDate).MustBeScheduleDate();
        validator.RuleFor(h => h.Description).MaximumLength(Holiday.DescriptionMaxLength);
        validator.RuleFor(h => h.WorkLocationIds)
            .Must(ids => ids!.Count <= MaxWorkLocations).WithMessage($"'{{PropertyName}}' can list at most {MaxWorkLocations} locations.")
            .Must(ids => ids!.Distinct().Count() == ids!.Count).WithMessage("'{PropertyName}' lists a location more than once.")
            .When(h => h.WorkLocationIds is not null);
        validator.RuleForEach(h => h.WorkLocationIds).NotEmpty();
    }

    public static void Apply(this Holiday holiday, IHolidayFields fields)
    {
        holiday.Name = fields.Name.Trim();
        holiday.HolidayDate = fields.HolidayDate;
        holiday.IsOptional = fields.IsOptional;
        holiday.Description = OrgCodes.NullIfBlank(fields.Description);
    }
}

internal sealed class CreateHolidayCommandValidator : AbstractValidator<CreateHolidayCommand>
{
    public CreateHolidayCommandValidator() => this.AddHolidayRules();
}

internal sealed class UpdateHolidayCommandValidator : AbstractValidator<UpdateHolidayCommand>
{
    public UpdateHolidayCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
        this.AddHolidayRules();
    }
}

internal sealed class DeleteHolidayCommandValidator : AbstractValidator<DeleteHolidayCommand>
{
    public DeleteHolidayCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
    }
}
