using Attendance.Domain.Common;

namespace Attendance.Domain.Identity;

public sealed class Permission : BaseEntity
{
    public const int CodeMaxLength = 100;
    public const int ModuleMaxLength = 50;
    public const int DescriptionMaxLength = 250;

    /// <summary>A stable code such as <c>leave.approve</c>. It is the permission's public identifier.</summary>
    public required string Code { get; set; }

    public required string Module { get; set; }

    public string? Description { get; set; }
}
