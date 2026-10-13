using Attendance.Domain.Common;

namespace Attendance.Domain.Identity;

public sealed class UserRole : AuditableEntity
{
    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public long RoleId { get; set; }

    public Role Role { get; set; } = null!;
}
