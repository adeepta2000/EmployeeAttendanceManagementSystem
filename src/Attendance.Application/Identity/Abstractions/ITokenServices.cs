namespace Attendance.Application.Identity.Abstractions;

/// <summary>Creates signed JWT access tokens.</summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(AccessTokenSubject subject, DateTime issuedAtUtc);
}

/// <summary>What an access token says about its holder. Only public IDs go into a token.</summary>
public sealed record AccessTokenSubject(Guid UserPublicId, Guid? EmployeePublicId, IReadOnlyCollection<string> Permissions);

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
