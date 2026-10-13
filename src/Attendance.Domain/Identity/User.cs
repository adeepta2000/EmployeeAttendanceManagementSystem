using Attendance.Domain.Common;
using Attendance.Domain.Organization;

namespace Attendance.Domain.Identity;

/// <summary>A login account. Admins may have no employee record; an employee has at most one user.</summary>
public sealed class User : BaseEntity, IHasPublicId
{
    public const int EmailMaxLength = 256;

    public Guid PublicId { get; private set; } = Guid.NewGuid();

    /// <summary>Stored normalized (see <see cref="NormalizeEmail"/>) so uniqueness doesn't depend on how it was typed.</summary>
    public required string Email
    {
        get;
        set => field = NormalizeEmail(value);
    }

    public required string PasswordHash { get; set; }

    public long? EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public DateTime? LastLoginUtc { get; set; }

    public ICollection<UserRole> UserRoles { get; } = [];

    /// <summary>The form <see cref="Email"/> is stored in. Look users up by the normalized value.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>True while a lockout from too many failed sign-ins is in force.</summary>
    public bool IsLockedOut(DateTime nowUtc) => LockoutEndUtc > nowUtc;

    /// <summary>
    /// Counts a failed sign-in. Reaching <paramref name="maxFailedAttempts"/> locks the account for
    /// <paramref name="lockoutDuration"/>. A lockout that has already expired starts a fresh count.
    /// </summary>
    /// <returns>True if this failure locked the account.</returns>
    public bool RegisterFailedLogin(DateTime nowUtc, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        if (LockoutEndUtc <= nowUtc)
        {
            LockoutEndUtc = null;
            FailedLoginCount = 0;
        }

        FailedLoginCount++;
        if (FailedLoginCount < maxFailedAttempts)
        {
            return false;
        }

        LockoutEndUtc = nowUtc + lockoutDuration;
        return true;
    }

    /// <summary>Clears the failure count and any expired lockout, and records the sign-in time.</summary>
    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        FailedLoginCount = 0;
        LockoutEndUtc = null;
        LastLoginUtc = nowUtc;
    }
}
