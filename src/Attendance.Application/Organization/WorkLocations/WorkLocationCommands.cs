using Attendance.Application.Common.Messaging;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Organization.WorkLocations;

/// <summary>
/// Adds a work location (FR-1.1). The geofence (FR-3.2) is optional: give latitude and longitude
/// together, and a radius only with them.
/// </summary>
public sealed record CreateWorkLocationCommand(
    string Code,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters)
    : ICommand<Result<WorkLocationDto>>, IWorkLocationFields;

public sealed record UpdateWorkLocationCommand(
    Guid PublicId,
    string Code,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters)
    : ICommand<Result<WorkLocationDto>>, IWorkLocationFields;

/// <summary>Retires or restores a work location. Retiring is refused while active employees are assigned to it.</summary>
public sealed record SetWorkLocationActiveCommand(Guid PublicId, bool IsActive) : ICommand<Result<WorkLocationDto>>;

internal interface IWorkLocationFields
{
    string Code { get; }

    string Name { get; }

    string? Address { get; }

    decimal? Latitude { get; }

    decimal? Longitude { get; }

    int? GeofenceRadiusMeters { get; }
}

internal static class WorkLocationRules
{
    public const int MaxGeofenceRadiusMeters = 10_000;

    public static void AddWorkLocationRules<T>(this AbstractValidator<T> validator)
        where T : IWorkLocationFields
    {
        validator.RuleFor(c => c.Code).MustBeCode(WorkLocation.CodeMaxLength);
        validator.RuleFor(c => c.Name).MustBeName(WorkLocation.NameMaxLength);
        validator.RuleFor(c => c.Address).MaximumLength(WorkLocation.AddressMaxLength);

        // DECIMAL(9,6) columns (spec §3.2): more decimals would be silently rounded.
        validator.RuleFor(c => c.Latitude).InclusiveBetween(-90m, 90m).PrecisionScale(9, 6, ignoreTrailingZeros: true);
        validator.RuleFor(c => c.Longitude).InclusiveBetween(-180m, 180m).PrecisionScale(9, 6, ignoreTrailingZeros: true);
        validator.RuleFor(c => c.Longitude).NotNull().When(c => c.Latitude.HasValue)
            .WithMessage("Give latitude and longitude together.");
        validator.RuleFor(c => c.Latitude).NotNull().When(c => c.Longitude.HasValue)
            .WithMessage("Give latitude and longitude together.");

        validator.RuleFor(c => c.GeofenceRadiusMeters).InclusiveBetween(1, MaxGeofenceRadiusMeters);
        validator.RuleFor(c => c.GeofenceRadiusMeters).Null().When(c => c.Latitude is null || c.Longitude is null)
            .WithMessage("A geofence radius needs the location's latitude and longitude.");
    }

    public static void Apply(this WorkLocation location, IWorkLocationFields fields)
    {
        location.Code = OrgCodes.Normalize(fields.Code);
        location.Name = fields.Name.Trim();
        location.Address = OrgCodes.NullIfBlank(fields.Address);
        location.Latitude = fields.Latitude;
        location.Longitude = fields.Longitude;
        location.GeofenceRadiusMeters = fields.GeofenceRadiusMeters;
    }
}

internal sealed class CreateWorkLocationCommandValidator : AbstractValidator<CreateWorkLocationCommand>
{
    public CreateWorkLocationCommandValidator() => this.AddWorkLocationRules();
}

internal sealed class UpdateWorkLocationCommandValidator : AbstractValidator<UpdateWorkLocationCommand>
{
    public UpdateWorkLocationCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
        this.AddWorkLocationRules();
    }
}

internal sealed class SetWorkLocationActiveCommandValidator : AbstractValidator<SetWorkLocationActiveCommand>
{
    public SetWorkLocationActiveCommandValidator()
    {
        RuleFor(c => c.PublicId).NotEmpty();
    }
}
