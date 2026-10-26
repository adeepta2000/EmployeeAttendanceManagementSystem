using Attendance.Domain.Common;

namespace Attendance.Domain.Identity;

/// <summary>
/// A one-time password-reset link (screen 3). Only the hash of the token is stored; the token itself goes
/// to the user's email and nowhere else. <c>CreatedAt</c> is the issue time.
/// </summary>
public sealed class PasswordResetToken : BaseEntity
{
    public long UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Hash of the token. The raw token is never stored.</summary>
    public required string TokenHash { get; set; }

    public required DateTime ExpiresUtc { get; set; }

    /// <summary>When the link set a new password. Set once, by <see cref="MarkUsed"/>.</summary>
    public DateTime? UsedUtc { get; private set; }

    /// <summary>When the link was cancelled unused: a newer link replaced it, or the password changed another way.</summary>
    public DateTime? RevokedUtc { get; private set; }

    public required string CreatedByIp { get; set; }

    /// <summary>True if the link can still set a password: never used, never revoked and not expired.</summary>
    public bool IsUsable(DateTime nowUtc) => UsedUtc is null && RevokedUtc is null && ExpiresUtc > nowUtc;

    public void MarkUsed(DateTime nowUtc)
    {
        if (UsedUtc is null && RevokedUtc is null)
        {
            UsedUtc = nowUtc;
        }
    }

    /// <summary>Cancels an unused link. A used or already revoked link keeps its original record.</summary>
    public void Revoke(DateTime nowUtc)
    {
        if (UsedUtc is null && RevokedUtc is null)
        {
            RevokedUtc = nowUtc;
        }
    }
}
