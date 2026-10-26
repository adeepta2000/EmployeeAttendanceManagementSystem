using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Attendance.Application.Common.Interfaces;

namespace Attendance.Api.Tests.Infrastructure;

/// <summary>Stands in for the email transport: keeps every message so a test can read it.</summary>
public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> SentTo(string address) =>
        [.. _sent.Where(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase))];

    /// <summary>The token of a reset link in an email body (<c>…/reset-password#token=…</c>).</summary>
    public static string ResetToken(EmailMessage message) =>
        ResetLink().Match(message.TextBody) is { Success: true } match
            ? match.Groups["token"].Value
            : throw new InvalidOperationException("The email has no reset link.");

    [GeneratedRegex(@"/reset-password#token=(?<token>[A-Za-z0-9_-]+)")]
    private static partial Regex ResetLink();
}
