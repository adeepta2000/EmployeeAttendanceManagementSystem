using Attendance.Domain.Common;

namespace Attendance.Domain.Identity;

/// <summary>A named bundle of permissions (FR-8.2). Authorization checks permissions, never role names.</summary>
public sealed class Role : BaseEntity, IHasPublicId
{
    public const int NameMaxLength = 50;
    public const int DescriptionMaxLength = 250;

    public Guid PublicId { get; private set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>True for the built-in roles of FR-8.1 (Admin, HRManager, HROfficer, Manager, Employee).</summary>
    public bool IsSystem { get; set; }

    public ICollection<RolePermission> RolePermissions { get; } = [];
}
