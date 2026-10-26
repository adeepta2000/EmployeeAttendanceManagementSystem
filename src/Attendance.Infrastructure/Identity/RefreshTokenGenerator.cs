using Attendance.Application.Identity.Abstractions;

namespace Attendance.Infrastructure.Identity;

internal sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    // 512 bits from the OS CSPRNG, encoded as 86 URL-safe characters.
    private const int TokenBytes = 64;

    public GeneratedRefreshToken Generate()
    {
        var token = SecretToken.Create(TokenBytes);
        return new GeneratedRefreshToken(token, Hash(token));
    }

    public string Hash(string token) => SecretToken.Hash(token);
}
