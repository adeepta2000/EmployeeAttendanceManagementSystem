namespace Attendance.Application.Identity.Abstractions;

/// <summary>Password hashing (spec §2.6: ASP.NET Identity's PBKDF2 hasher).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheck Verify(string passwordHash, string providedPassword);

    /// <summary>
    /// Does the same work as <see cref="Verify"/> against a throwaway hash and ignores the result.
    /// Rejecting an unknown, inactive or locked account then takes as long as rejecting a wrong
    /// password, so response timing doesn't reveal which accounts exist.
    /// </summary>
    void SimulateVerify(string providedPassword);
}

public enum PasswordCheck
{
    Failed,
    Success,

    /// <summary>Correct, but hashed with outdated parameters: store a fresh hash.</summary>
    SuccessRehashNeeded,
}
