using Attendance.Domain.Common;

namespace Attendance.Domain.Identity;

/// <summary>One link in a rotating refresh-token chain (spec §2.5). <c>CreatedAt</c> is the issue time.</summary>
public sealed class RefreshToken : BaseEntity
{
    public const int TokenHashMaxLength = 256;
    public const int IpAddressMaxLength = 45;
    public const int UserAgentMaxLength = 400;

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Hash of the token. The raw token is never stored.</summary>
    public required string TokenHash { get; set; }

    /// <summary>Shared by every token in one rotation chain. Reusing a revoked token revokes the whole family.</summary>
    public required Guid FamilyId { get; set; }

    public required DateTime ExpiresUtc { get; set; }

    /// <summary>Set once, by <see cref="Revoke"/>. A token is never un-revoked.</summary>
    public DateTime? RevokedUtc { get; private set; }

    /// <summary>The hash of the token this one was rotated into; null if it was revoked without a successor.</summary>
    public string? ReplacedByTokenHash { get; private set; }

    public required string CreatedByIp { get; set; }

    public string? UserAgent { get; set; }

    public bool IsRevoked => RevokedUtc is not null;

    public bool IsExpired(DateTime nowUtc) => ExpiresUtc <= nowUtc;

    /// <summary>True if the token can still be exchanged: neither revoked nor expired.</summary>
    public bool IsActive(DateTime nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    /// <summary>
    /// Revokes the token, recording its successor when it was rotated. Revoking an already revoked
    /// token changes nothing, so the original revocation time and rotation chain are kept.
    /// </summary>
    public void Revoke(DateTime nowUtc, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedUtc = nowUtc;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
