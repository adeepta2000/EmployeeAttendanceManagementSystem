namespace Attendance.Application.Identity.Abstractions;

/// <summary>Creates signed JWT access tokens.</summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(AccessTokenSubject subject, DateTime issuedAtUtc);
}

/// <summary>What an access token says about its holder. Only public IDs go into a token.</summary>
/// <param name="MustChangePassword">Restricts the token to the change-password endpoint (screen 4).</param>
public sealed record AccessTokenSubject(
    Guid UserPublicId,
    Guid? EmployeePublicId,
    IReadOnlyCollection<string> Permissions,
    bool MustChangePassword = false);

public sealed record AccessToken(string Token, DateTime ExpiresUtc)
{
    public override string ToString() => nameof(AccessToken);
}

/// <summary>Creates opaque refresh tokens and hashes them for storage.</summary>
public interface IRefreshTokenGenerator
{
    /// <summary>A new random token and its hash. Return the token to the client and store only the hash.</summary>
    GeneratedRefreshToken Generate();

    /// <summary>The stored form of <paramref name="token"/>, for looking a presented token up.</summary>
    string Hash(string token);
}

public sealed record GeneratedRefreshToken(string Token, string Hash)
{
    public override string ToString() => nameof(GeneratedRefreshToken);
}

/// <summary>Creates the one-time tokens of password-reset links and hashes them for storage.</summary>
public interface IPasswordResetTokenGenerator
{
    /// <summary>A new random token and its hash. Email the token and store only the hash.</summary>
    GeneratedResetToken Generate();

    /// <summary>The stored form of <paramref name="token"/>, for looking a presented token up.</summary>
    string Hash(string token);
}

public sealed record GeneratedResetToken(string Token, string Hash)
{
    public override string ToString() => nameof(GeneratedResetToken);
}
