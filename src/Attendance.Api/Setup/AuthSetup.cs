using System.Text;
using Attendance.Application.Identity;
using Attendance.Domain.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Attendance.Api.Setup;

internal static class AuthSetup
{
    /// <summary>
    /// Binds and validates the auth settings at startup, and validates bearer access tokens. The API
    /// reads the token from the Authorization header; the BFF takes it from its httpOnly cookie.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>()
            .BindConfiguration(AuthOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep claim names as issued ("sub", "permission") instead of mapping them to long URIs.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),

                    // Pin the algorithm so a token can't pick a weaker one (or "none").
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,

                    // The default 5 minutes would stretch a 15-minute token by a third.
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AuthClaimTypes.Subject,
                };
            });

        return services;
    }

    /// <summary>
    /// One policy per permission code (FR-8.2), named by the code, so endpoints use
    /// <c>[Authorize(Policy = Permissions.Leave.Approve)]</c>. Endpoints without an attribute still need
    /// a signed-in user (the fallback policy); public ones opt out with <c>[AllowAnonymous]</c>.
    /// </summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        var builder = services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        foreach (var permission in Permissions.All)
        {
            builder.AddPolicy(permission.Code, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AuthClaimTypes.Permission, permission.Code));
        }

        return services;
    }
}
