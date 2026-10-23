using Attendance.Domain.Common;

namespace Attendance.Domain.Organization;

public sealed class Department : BaseEntity, IMasterData
{
    public const int CodeMaxLength = 30;
    public const int NameMaxLength = 100;

    public Guid PublicId { get; private set; } = Guid.NewGuid();

    public required string Code { get; set; }

    public required string Name { get; set; }

    public long? ParentDepartmentId { get; set; }

    public Department? ParentDepartment { get; set; }

    public long? HeadEmployeeId { get; set; }

    public Employee? HeadEmployee { get; set; }

    public bool IsActive { get; set; } = true;
}
