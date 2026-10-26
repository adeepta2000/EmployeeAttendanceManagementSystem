namespace Attendance.Application.Common.Interfaces;

/// <summary>Delivers email. The sender address and the transport are configuration (Infrastructure).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>A plain-text email to one recipient.</summary>
public sealed record EmailMessage(string To, string Subject, string TextBody)
{
    // The body can carry a one-time secret, such as a reset link: keep it out of logs.
    public override string ToString() => nameof(EmailMessage);
}
