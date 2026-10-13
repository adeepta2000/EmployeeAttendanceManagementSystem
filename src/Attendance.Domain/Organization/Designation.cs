using Attendance.Domain.Common;

namespace Attendance.Domain.Organization;

public sealed class Designation : BaseEntity, IHasPublicId
{
    public const int CodeMaxLength = 30;
    public const int NameMaxLength = 100;
    public const int GradeMaxLength = 20;

    public Guid PublicId { get; private set; } = Guid.NewGuid();

    public required string Code { get; set; }

    public required string Name { get; set; }

    public string? Grade { get; set; }

    public bool IsActive { get; set; } = true;
}
