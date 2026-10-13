using Attendance.Api.Contracts.Auth;
using Attendance.Api.Services;
using Attendance.Api.Setup;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Identity;
using Attendance.Application.Identity.Login;
using Attendance.Application.Identity.Logout;
using Attendance.Application.Identity.Refresh;
using Attendance.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Attendance.Api.Controllers;

/// <summary>
/// Sign-in, token refresh and sign-out (spec §2.5). Tokens travel in the response body; the Next.js
/// Route Handlers keep them in httpOnly cookies, so these endpoints never set cookies themselves.
/// </summary>
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Signs in and starts a session: a 15-minute access token and a 7-day refresh token.</summary>
    /// <response code="401">Wrong email or password, or the account is locked or inactive. The response doesn't say which.</response>
    /// <response code="429">More than 5 sign-in attempts from this IP address in the last minute.</response>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingSetup.LoginPolicy)]
    [ProducesResponseType<AuthTokens>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        [FromServices] ICommandHandler<LoginCommand, Result<AuthTokens>> handler,
        CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password, HttpContext.GetClientIp(), HttpContext.GetUserAgent());
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>
    /// Exchanges a refresh token for a new token pair. The presented token is revoked; presenting it
    /// again revokes the whole session and forces a new sign-in.
    /// </summary>
    /// <response code="401">The refresh token is unknown, expired or revoked. Sign in again.</response>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthTokens>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Refresh(
        RefreshTokenRequest request,
        [FromServices] ICommandHandler<RefreshTokenCommand, Result<AuthTokens>> handler,
        CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(request.RefreshToken, HttpContext.GetClientIp(), HttpContext.GetUserAgent());
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>
    /// Ends the session the refresh token belongs to. Always succeeds, even for an unknown token. The
    /// access token stays valid until it expires, so the client must discard it too.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Logout(
        LogoutRequest request,
        [FromServices] ICommandHandler<LogoutCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new LogoutCommand(request.RefreshToken), cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }
}
