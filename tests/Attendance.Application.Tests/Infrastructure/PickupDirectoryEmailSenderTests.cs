using System.Text;
using Attendance.Application.Common.Interfaces;
using Attendance.Application.Tests.Persistence;
using Attendance.Infrastructure.Email;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Attendance.Application.Tests.Infrastructure;

public sealed class PickupDirectoryEmailSenderTests : IDisposable
{
    private const string From = "Nordwin Attendance <no-reply@nordwin.example>";

    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"pickup-tests-{Guid.NewGuid():N}");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Writes_each_message_as_an_eml_file_under_the_content_root()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 26, 9, 30, 0, TimeSpan.Zero));
        var sender = new PickupDirectoryEmailSender(
            Options.Create(new EmailOptions { From = From, PickupDirectory = "App_Data/mail" }),
            new TestHostEnvironment(_contentRoot),
            clock,
            NullLogger<PickupDirectoryEmailSender>.Instance);

        await sender.SendAsync(new EmailMessage("rafiq.islam@nordwin.example", "Reset your password", "Hello Rafiq,\nOpen this link."), Ct);

        var file = Directory.GetFiles(Path.Combine(_contentRoot, "App_Data", "mail"), "*.eml").Should().ContainSingle().Subject;
        Path.GetFileName(file).Should().StartWith("20261026T093000000-");
        var text = await File.ReadAllTextAsync(file, Ct);
        text.Should().Contain("To: rafiq.islam@nordwin.example\r\n")
            .And.Contain("Date: Mon, 26 Oct 2026 09:30:00 GMT\r\n")
            .And.Contain("\r\n\r\nHello Rafiq,\r\nOpen this link.\r\n");
    }

    [Fact]
    public void Composes_a_plain_text_message_with_an_encoded_subject()
    {
        var message = new EmailMessage("someone@nordwin.example", "Reset — your password", "Line one\nLine two");

        var text = PickupDirectoryEmailSender.Compose(message, From, DateTimeOffset.UnixEpoch, "abc123");

        var subject = Convert.ToBase64String(Encoding.UTF8.GetBytes("Reset — your password"));
        text.Should().StartWith($"From: {From}\r\n")
            .And.Contain($"Subject: =?utf-8?B?{subject}?=\r\n")
            .And.Contain("Message-ID: <abc123@nordwin-attendance>\r\n")
            .And.Contain("Content-Type: text/plain; charset=utf-8\r\n")
            .And.EndWith("\r\n\r\nLine one\r\nLine two\r\n");
    }

    [Theory]
    [InlineData("victim@nordwin.example\r\nBcc: everyone@nordwin.example")]
    [InlineData("not-an-address")]
    public void Refuses_a_recipient_that_could_inject_headers(string to)
    {
        var compose = () => PickupDirectoryEmailSender.Compose(new EmailMessage(to, "Subject", "Body"), From, DateTimeOffset.UnixEpoch, "id");

        compose.Should().Throw<ArgumentException>();
    }

    private sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Attendance.Tests";

        public string ContentRootPath { get; set; } = contentRoot;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
