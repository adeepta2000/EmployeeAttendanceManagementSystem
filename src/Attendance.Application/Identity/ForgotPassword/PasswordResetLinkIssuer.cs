using Attendance.Application.Common.Interfaces;
using Attendance.Application.Identity.Abstractions;
using Attendance.Domain.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Attendance.Application.Identity.ForgotPassword;

/// <summary>
/// Creates a password-reset link and emails it. Runs as background work queued by
/// <see cref="ForgotPasswordCommandHandler"/>, after the caller has had their answer.
/// </summary>
internal sealed class PasswordResetLinkIssuer(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    IPasswordResetTokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider,
    ILogger<PasswordResetLinkIssuer> logger)
{
    public async Task IssueAsync(string email, string clientIp, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = await users.FindByEmailAsync(email, cancellationToken);
        if (user is null || !user.IsActive)
        {
            logger.LogInformation(
                "Password reset requested from {ClientIp} for an unknown or inactive account; no email sent", clientIp);
            return;
        }

        // Only the newest link works: asking again cancels the earlier ones.
        foreach (var earlier in await resetTokens.GetOutstandingForUserAsync(user.Id, cancellationToken))
        {
            earlier.Revoke(now);
        }

        var policy = options.Value;
        var token = tokenGenerator.Generate();
        var link = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = token.Hash,
            ExpiresUtc = now + policy.PasswordResetLinkLifetime,
            CreatedByIp = clientIp,
        };
        resetTokens.Add(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailSender.SendAsync(
            PasswordResetEmail.Create(user, ResetUrl(policy.ClientBaseUrl, token.Token), policy.PasswordResetLinkLifetime),
            cancellationToken);

        logger.LogInformation(
            "Password reset link for user {UserId} sent; it works until {ExpiresUtc:o}", user.PublicId, link.ExpiresUtc);
    }

    /// <summary>
    /// The client's reset screen, with the token in the fragment: browsers never send a fragment to a
    /// server, so the token stays out of access logs and Referer headers.
    /// </summary>
    private static string ResetUrl(string clientBaseUrl, string token) =>
        $"{clientBaseUrl.TrimEnd('/')}/reset-password#token={token}";
}

internal static class PasswordResetEmail
{
    public const string Subject = "Reset your Nordwin Attendance password";

    public static EmailMessage Create(User user, string resetUrl, TimeSpan lifetime)
    {
        var greeting = user.Employee is { } employee ? $"Hello {employee.FirstName}," : "Hello,";
        var body = $"""
            {greeting}

            Someone asked to reset the password of your Nordwin Attendance account.
            If it was you, open this link within {Math.Ceiling(lifetime.TotalMinutes):0} minutes and choose a new password:

            {resetUrl}

            The link works once. Using it signs you out everywhere you are signed in.

            If you didn't ask for this, ignore this email: your password stays as it is.

            Nordwin Attendance
            """;
        return new EmailMessage(user.Email, Subject, body);
    }
}
