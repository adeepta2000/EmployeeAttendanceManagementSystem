using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Attendance.Infrastructure.Identity;

/// <summary>Random bearer secrets (refresh tokens, reset links) that are stored only as a hash.</summary>
internal static class SecretToken
{
    public static string Create(int bytes) => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(bytes));

    // A fast, unsalted hash is enough here, unlike for passwords: the token is at least 256 random bits,
    // so it can't be recovered from a leaked hash by guessing. Hex SHA-256 is 64 characters.
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
