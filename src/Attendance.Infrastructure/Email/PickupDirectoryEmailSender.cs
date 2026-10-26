using System.Text;
using Attendance.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Attendance.Infrastructure.Email;

/// <summary>Email settings. Bound from the <c>Email</c> section.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>The From header, e.g. <c>Nordwin Attendance &lt;no-reply@nordwin.example&gt;</c>.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Where messages are written as .eml files, one per email. A relative path is under the API's content
    /// root. Outside the web root and gitignored: the files hold one-time secrets such as reset links.
    /// </summary>
    public string PickupDirectory { get; set; } = "App_Data/mail";

    internal bool IsValid => !string.IsNullOrWhiteSpace(From) && !From.AsSpan().ContainsAny('\r', '\n')
        && !string.IsNullOrWhiteSpace(PickupDirectory);

    internal const string ValidationMessage = "Email:From must be set (one line), and Email:PickupDirectory must not be empty.";
}

/// <summary>
/// Writes each email as an RFC 5322 .eml file to a pickup directory instead of sending it. Any mail client
/// opens the files, and an SMTP relay can collect them. There is no SMTP transport yet: until there is,
/// nothing leaves the server, and outside Development every send logs a warning saying so.
/// </summary>
internal sealed class PickupDirectoryEmailSender(
    IOptions<EmailOptions> options,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<PickupDirectoryEmailSender> logger)
    : IEmailSender
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var directory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, settings.PickupDirectory));
        Directory.CreateDirectory(directory);

        var sentAt = timeProvider.GetUtcNow();
        var messageId = Guid.NewGuid().ToString("N");
        var path = Path.Combine(directory, $"{sentAt:yyyyMMdd'T'HHmmssfff}-{messageId}.eml");
        await File.WriteAllTextAsync(path, Compose(message, settings.From, sentAt, messageId), Utf8, cancellationToken);

        // The recipient and the content stay out of the log: the address is personal data, and the body
        // may hold a reset link.
        if (environment.IsDevelopment())
        {
            logger.LogInformation("Email {MessageId} written to {PickupDirectory}", messageId, directory);
        }
        else
        {
            logger.LogWarning(
                "Email {MessageId} written to {PickupDirectory} and not delivered: no mail transport is configured",
                messageId, directory);
        }
    }

    /// <summary>
    /// A plain-text UTF-8 message. The body goes out as 8bit text, so the file stays readable in an
    /// editor; the subject is an RFC 2047 encoded word, so no header value can break a line.
    /// </summary>
    internal static string Compose(EmailMessage message, string from, DateTimeOffset sentAt, string messageId)
    {
        if (message.To.AsSpan().ContainsAny('\r', '\n') || !message.To.Contains('@'))
        {
            throw new ArgumentException("The recipient must be a single email address.", nameof(message));
        }

        var subject = Convert.ToBase64String(Encoding.UTF8.GetBytes(message.Subject));
        var body = message.TextBody.ReplaceLineEndings("\r\n");

        return new StringBuilder()
            .Append("From: ").Append(from).Append("\r\n")
            .Append("To: ").Append(message.To).Append("\r\n")
            .Append("Subject: =?utf-8?B?").Append(subject).Append("?=\r\n")
            .Append("Date: ").Append(sentAt.ToString("r")).Append("\r\n")
            .Append("Message-ID: <").Append(messageId).Append("@nordwin-attendance>\r\n")
            .Append("MIME-Version: 1.0\r\n")
            .Append("Content-Type: text/plain; charset=utf-8\r\n")
            .Append("Content-Transfer-Encoding: 8bit\r\n")
            .Append("\r\n")
            .Append(body)
            .Append("\r\n")
            .ToString();
    }
}
