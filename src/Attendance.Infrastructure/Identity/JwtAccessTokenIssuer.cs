using System.Security.Claims;
using System.Text;
using Attendance.Application.Identity;
using Attendance.Application.Identity.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Attendance.Infrastructure.Identity;

/// <summary>
/// Issues HMAC-SHA256 signed JWTs. The claims are the user's PublicId, their employee's PublicId and
/// one claim per permission: no internal IDs, roles or personal data.
/// </summary>
internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> jwtOptions, IOptions<AuthOptions> authOptions)
    : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(AccessTokenSubject subject, DateTime issuedAtUtc)
    {
        var jwt = jwtOptions.Value;
        var expiresUtc = issuedAtUtc + authOptions.Value.AccessTokenLifetime;

        var claims = new List<Claim>
        {
            new(AuthClaimTypes.Subject, subject.UserPublicId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (subject.EmployeePublicId is { } employeeId)
        {
            claims.Add(new Claim(AuthClaimTypes.EmployeeId, employeeId.ToString()));
        }

        claims.AddRange(subject.Permissions.Select(code => new Claim(AuthClaimTypes.Permission, code)));
        if (subject.MustChangePassword)
        {
            claims.Add(new Claim(AuthClaimTypes.PasswordChangeRequired, "true"));
        }

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAtUtc,
            NotBefore = issuedAtUtc,
            Expires = expiresUtc,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expiresUtc);
    }
}
