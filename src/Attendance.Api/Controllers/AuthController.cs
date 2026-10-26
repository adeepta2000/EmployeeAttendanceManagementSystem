using Attendance.Api.Authorization;
using Attendance.Api.Contracts.Auth;
using Attendance.Api.Services;
using Attendance.Api.Setup;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Identity;
using Attendance.Application.Identity.ChangePassword;
using Attendance.Application.Identity.ForgotPassword;
using Attendance.Application.Identity.Login;
using Attendance.Application.Identity.Logout;
using Attendance.Application.Identity.Refresh;
using Attendance.Application.Identity.ResetPassword;
using Attendance.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Attendance.Api.Controllers;

/// <summary>
/// Sign-in, token refresh, sign-out and passwords (spec §2.5, screens 1 to 4). Tokens travel in the
/// response body; the Next.js Route Handlers keep them in httpOnly cookies, so these endpoints never set
/// cookies themselves.
/// </summary>
[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Signs in and starts a session: a 15-minute access token and a 7-day refresh token.</summary>
    /// <response code="401">Wrong email or password, or the account is locked or inactive. The response doesn't say which.</response>
    /// <response code="429">More than 5 sign-in attempts from this IP address in the last minute.</response>
    [HttpPost("login")]
    [AllowAnonymous]
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
    [AllowAnonymous]
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
    [AllowAnonymous]
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

    /// <summary>
    /// Changes the signed-in user's password (screen 4). It is the one endpoint open to an account that
    /// must change its password. Every session of the account ends, and the response starts a new one.
    /// </summary>
    /// <response code="400">The new password breaks the policy, the current password is wrong
    /// (<c>auth.invalid_current_password</c>), or too many wrong attempts locked the account (<c>auth.locked_out</c>).</response>
    /// <response code="401">Not signed in, or the account is inactive.</response>
    [HttpPost("change-password")]
    [Authorize]
    [AllowWhilePasswordChangeRequired]
    [ProducesResponseType<AuthTokens>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        [FromServices] ICommandHandler<ChangePasswordCommand, Result<AuthTokens>> handler,
        CancellationToken cancellationToken)
    {
        var command = new ChangePasswordCommand(
            request.CurrentPassword, request.NewPassword, HttpContext.GetClientIp(), HttpContext.GetUserAgent());
        return ToActionResult(await handler.HandleAsync(command, cancellationToken));
    }

    /// <summary>
    /// Emails a one-time password-reset link if the address belongs to an active account (screen 2).
    /// The answer is the same 202 either way, and arrives before any work is done, so neither it nor its
    /// timing reveals which accounts exist.
    /// </summary>
    /// <response code="429">More than 5 password-reset requests from this IP address in the last minute.</response>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.PasswordResetPolicy)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        [FromServices] ICommandHandler<ForgotPasswordCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ForgotPasswordCommand(request.Email, HttpContext.GetClientIp()), cancellationToken);
        return result.IsSuccess ? Accepted() : Problem(result.Error);
    }

    /// <summary>
    /// Sets a new password with the token of a reset link (screen 3). It ends every session of the
    /// account and lifts any lockout; the user then signs in with the new password.
    /// </summary>
    /// <response code="400">The new password breaks the policy, or the link is unknown, used, replaced or
    /// expired (<c>auth.invalid_reset_token</c>).</response>
    /// <response code="429">More than 5 password-reset requests from this IP address in the last minute.</response>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.PasswordResetPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        [FromServices] ICommandHandler<ResetPasswordCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var command = new ResetPasswordCommand(request.Token, request.NewPassword, HttpContext.GetClientIp());
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }
}
