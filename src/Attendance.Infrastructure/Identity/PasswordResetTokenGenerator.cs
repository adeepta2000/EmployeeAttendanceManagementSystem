using Attendance.Application.Identity.Abstractions;

namespace Attendance.Infrastructure.Identity;

internal sealed class PasswordResetTokenGenerator : IPasswordResetTokenGenerator
{
    // 256 bits from the OS CSPRNG, encoded as 43 URL-safe characters: short enough for a link, far
    // beyond guessing.
    private const int TokenBytes = 32;

    public GeneratedResetToken Generate()
    {
        var token = SecretToken.Create(TokenBytes);
        return new GeneratedResetToken(token, Hash(token));
    }

    public string Hash(string token) => SecretToken.Hash(token);
}
