namespace Attendance.Domain.Common;

/// <summary>
/// A transactional row that is never hard-deleted. Removing it sets <see cref="IsDeleted"/>, and a
/// global query filter hides it from queries. Master data is retired with <c>IsActive = false</c> instead.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }
}
