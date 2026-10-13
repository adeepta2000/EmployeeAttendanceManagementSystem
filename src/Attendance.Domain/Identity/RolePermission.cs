using Attendance.Domain.Common;

namespace Attendance.Domain.Identity;

public sealed class RolePermission : AuditableEntity
{
    public long RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public long PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}
