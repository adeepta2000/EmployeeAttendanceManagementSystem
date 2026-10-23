using Attendance.Domain.Common;

namespace Attendance.Domain.Organization;

public sealed class WorkLocation : BaseEntity, IMasterData
{
    public const int CodeMaxLength = 30;
    public const int NameMaxLength = 100;
    public const int AddressMaxLength = 500;

    public Guid PublicId { get; private set; } = Guid.NewGuid();

    public required string Code { get; set; }

    public required string Name { get; set; }

    public string? Address { get; set; }

    // The geofence check is optional (FR-3.2): these stay null for a location without one.
    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public int? GeofenceRadiusMeters { get; set; }

    public bool IsActive { get; set; } = true;
}
