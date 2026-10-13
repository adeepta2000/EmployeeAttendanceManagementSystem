using Attendance.Application.Identity.Abstractions;
using Attendance.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Attendance.Infrastructure.Identity;

/// <summary>
/// ASP.NET Identity's password hasher (spec §2.6): PBKDF2 with HMAC-SHA512, 100,000 iterations and a
/// random salt per hash. Its default implementation never reads the user argument, so none is passed.
/// </summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    // Hashed with the same parameters as real passwords, so verifying against it costs the same.
    private readonly Lazy<string> _throwawayHash;

    public IdentityPasswordHasher() =>
        _throwawayHash = new(() => _hasher.HashPassword(null!, Guid.NewGuid().ToString()));

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public PasswordCheck Verify(string passwordHash, string providedPassword) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, providedPassword) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed,
        };

    public void SimulateVerify(string providedPassword) =>
        _hasher.VerifyHashedPassword(null!, _throwawayHash.Value, providedPassword);
}
