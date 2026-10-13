using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Attendance.Application.Identity.Abstractions;

namespace Attendance.Infrastructure.Identity;

internal sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    // 512 bits from the OS CSPRNG, encoded as 86 URL-safe characters.
    private const int TokenBytes = 64;

    public GeneratedRefreshToken Generate()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        return new GeneratedRefreshToken(token, Hash(token));
    }

    // A fast, unsalted hash is enough here, unlike for passwords: the token is 512 random bits, so it
    // can't be recovered from a leaked hash by guessing. Hex SHA-256 is 64 characters.
    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
