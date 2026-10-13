namespace Attendance.Domain.Common;

/// <summary>
/// An externally exposed entity. API routes identify it by <see cref="PublicId"/>, a random UUID,
/// so callers can't enumerate records by guessing sequential IDs.
/// </summary>
public interface IHasPublicId
{
    Guid PublicId { get; }
}
