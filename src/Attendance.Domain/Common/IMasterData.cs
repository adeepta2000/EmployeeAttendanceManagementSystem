namespace Attendance.Domain.Common;

/// <summary>
/// Reference data maintained on a master-data screen, identified by a unique business <see cref="Code"/>.
/// It is never deleted: retiring it sets <see cref="IsActive"/> to false, so history keeps pointing at it.
/// </summary>
public interface IMasterData : IHasPublicId
{
    string Code { get; }

    string Name { get; }

    bool IsActive { get; }
}
