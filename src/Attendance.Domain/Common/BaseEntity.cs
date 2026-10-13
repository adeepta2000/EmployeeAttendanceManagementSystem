namespace Attendance.Domain.Common;

/// <summary>
/// Base for every table with a surrogate key. <see cref="Id"/> (BIGINT IDENTITY) is for joins and
/// foreign keys only; it never appears in a route or response DTO.
/// </summary>
public abstract class BaseEntity : AuditableEntity
{
    public long Id { get; private set; }
}
