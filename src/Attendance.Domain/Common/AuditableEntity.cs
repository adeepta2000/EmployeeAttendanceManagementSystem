namespace Attendance.Domain.Common;

/// <summary>
/// Audit columns carried by every table (spec §3.2). The persistence layer stamps them on save,
/// so the setters are private: handlers and clients can never set them.
/// </summary>
public abstract class AuditableEntity
{
    public const int ActorMaxLength = 100;

    /// <summary>UTC instant the row was inserted.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>The acting user's PublicId, or <c>system</c> for background work.</summary>
    public string CreatedBy { get; private set; } = string.Empty;

    /// <summary>UTC instant of the last update; null until the row is first modified.</summary>
    public DateTime? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }
}
