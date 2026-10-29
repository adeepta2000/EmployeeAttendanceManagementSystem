using Attendance.Domain.Common;
using Attendance.Domain.Organization;

namespace Attendance.Domain.Scheduling;

/// <summary>
/// A public holiday (FR-7.1). It applies at every work location unless it is limited to some, e.g. a
/// holiday observed in Dhaka but not in Chittagong. Holidays drive both the attendance status and the
/// OT multiplier (FR-7.2).
/// </summary>
public sealed class Holiday : BaseEntity, IHasPublicId, ISoftDeletable
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private readonly List<HolidayLocation> _locations = [];

    public Guid PublicId { get; private set; } = Guid.NewGuid();

    public required string Name { get; set; }

    /// <summary>Setting it also sets <see cref="Year"/>.</summary>
    public required DateOnly HolidayDate
    {
        get;
        set
        {
            field = value;
            Year = value.Year;
        }
    }

    /// <summary>The year of <see cref="HolidayDate"/>, stored for the yearly calendar. Never set directly.</summary>
    public int Year { get; private set; }

    /// <summary>
    /// An optional (restricted) holiday is listed in the calendar but doesn't make the day a holiday:
    /// an employee who takes it does so as leave.
    /// </summary>
    public bool IsOptional { get; set; }

    public string? Description { get; set; }

    public bool IsDeleted { get; private set; }

    /// <summary>
    /// The location links as loaded. This includes removed ones when they were loaded, which
    /// <see cref="SetLocations"/> needs; use <see cref="WorkLocationIds"/> for the current set.
    /// </summary>
    public IReadOnlyCollection<HolidayLocation> Locations => _locations;

    /// <summary>The work locations the holiday is limited to. Empty means it applies everywhere.</summary>
    public IEnumerable<long> WorkLocationIds => _locations.Where(l => !l.IsDeleted).Select(l => l.WorkLocationId);

    public bool AppliesTo(long workLocationId)
    {
        var limitedTo = WorkLocationIds.ToList();
        return limitedTo.Count == 0 || limitedTo.Contains(workLocationId);
    }

    /// <summary>
    /// Limits the holiday to these work locations; an empty set makes it apply everywhere. A link that is
    /// no longer wanted is soft-deleted and comes back if the location is listed again, so every existing
    /// link, removed ones included, must be loaded first.
    /// </summary>
    public void SetLocations(IEnumerable<long> workLocationIds)
    {
        var wanted = workLocationIds.ToHashSet();
        foreach (var link in _locations)
        {
            if (wanted.Remove(link.WorkLocationId))
            {
                link.Restore();
            }
            else
            {
                link.Remove();
            }
        }

        _locations.AddRange(wanted.Select(id => new HolidayLocation(id)));
    }

    /// <summary>Removes the holiday from the calendar (a soft delete).</summary>
    public void Remove() => IsDeleted = true;
}

/// <summary>Limits a <see cref="Holiday"/> to one work location. Soft-deleted when the location is unlisted.</summary>
public sealed class HolidayLocation : AuditableEntity, ISoftDeletable
{
    internal HolidayLocation(long workLocationId) => WorkLocationId = workLocationId;

    public long HolidayId { get; private set; }

    public Holiday Holiday { get; private set; } = null!;

    public long WorkLocationId { get; private set; }

    public WorkLocation WorkLocation { get; private set; } = null!;

    public bool IsDeleted { get; private set; }

    internal void Remove() => IsDeleted = true;

    internal void Restore() => IsDeleted = false;
}
