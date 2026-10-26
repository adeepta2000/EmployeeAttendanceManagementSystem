using System.Net;
using Attendance.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public sealed class PasswordResetTests(AttendanceApiFactory api)
{
    private const string NewPassword = "Reset-Nordwin-Pa55phrase";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Forgot_password_emails_a_one_time_link_to_an_active_account()
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();

        (await client.ForgotPasswordAsync(user.Email)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        await api.DrainBackgroundWorkAsync();

        var email = api.Emails.SentTo(user.Email).Should().ContainSingle().Subject;
        email.Subject.Should().Be("Reset your Nordwin Attendance password");
        email.TextBody.Should().Contain($"{AttendanceApiFactory.ClientBaseUrl}/reset-password#token=",
            "the token travels in the fragment, which browsers never send to a server");

        // Only the hash is stored, valid for 30 minutes.
        var token = CapturingEmailSender.ResetToken(email);
        var stored = await api.QueryDbAsync(db => db.PasswordResetTokens.SingleAsync(t => t.User.PublicId == user.PublicId, Ct));
        stored.TokenHash.Should().Be(AuthApi.Sha256Hex(token)).And.NotBe(token);
        stored.ExpiresUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1));
        stored.UsedUtc.Should().BeNull();
    }

    [Fact]
    public async Task Unknown_and_inactive_addresses_get_the_same_202_and_no_email()
    {
        var inactive = await api.CreateUserAsync(isActive: false);
        using var client = api.CreateApiClient();

        (await client.ForgotPasswordAsync(inactive.Email)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await client.ForgotPasswordAsync("nobody.at.all@nordwin.example")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        await api.DrainBackgroundWorkAsync();

        api.Emails.SentTo(inactive.Email).Should().BeEmpty();
        api.Emails.SentTo("nobody.at.all@nordwin.example").Should().BeEmpty();
    }

    [Fact]
    public async Task Resetting_sets_the_password_ends_every_session_and_lifts_a_lockout()
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();
        var session = await client.SignInAsync(user);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var attacker = api.CreateApiClient();
            await attacker.LoginAsync(user.Email, $"guess-{attempt}");
        }

        var token = await RequestResetTokenAsync(user);
        var response = await client.ResetPasswordAsync(token, NewPassword);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.RefreshAsync(session.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "every session ends");
        (await client.LoginAsync(user.Email)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the old password is gone");
        (await client.LoginAsync(user.Email, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK, "the lockout is lifted");

        var stored = await api.QueryDbAsync(db => db.PasswordResetTokens.SingleAsync(t => t.User.PublicId == user.PublicId, Ct));
        stored.UsedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task A_link_works_only_once()
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();
        var token = await RequestResetTokenAsync(user);

        (await client.ResetPasswordAsync(token, NewPassword)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var again = await client.ResetPasswordAsync(token, "Another-Pa55phrase-2");

        (await again.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("auth.invalid_reset_token");
        (await client.LoginAsync(user.Email, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Asking_again_cancels_the_earlier_link()
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();
        var first = await RequestResetTokenAsync(user);
        var second = await RequestResetTokenAsync(user);

        (await (await client.ResetPasswordAsync(first, NewPassword)).ReadProblemCodeAsync(HttpStatusCode.BadRequest))
            .Should().Be("auth.invalid_reset_token");
        (await client.ResetPasswordAsync(second, NewPassword)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task An_expired_link_is_refused()
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();
        var token = await RequestResetTokenAsync(user);
        await api.QueryDbAsync(db => db.PasswordResetTokens
            .Where(t => t.User.PublicId == user.PublicId)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.ExpiresUtc, DateTime.UtcNow.AddSeconds(-1)), Ct));

        var response = await client.ResetPasswordAsync(token, NewPassword);

        (await response.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("auth.invalid_reset_token");
        (await client.LoginAsync(user.Email)).StatusCode.Should().Be(HttpStatusCode.OK, "the password is unchanged");
    }

    [Fact]
    public async Task A_link_stops_working_once_the_password_changes_another_way()
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();
        var token = await RequestResetTokenAsync(user);
        var session = await client.SignInAsync(user);

        (await client.ChangePasswordAsync(session.AccessToken, AttendanceApiFactory.Password, NewPassword))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await (await client.ResetPasswordAsync(token, "Another-Pa55phrase-2")).ReadProblemCodeAsync(HttpStatusCode.BadRequest))
            .Should().Be("auth.invalid_reset_token");
    }

    [Fact]
    public async Task An_unknown_token_and_a_short_password_are_refused()
    {
        using var client = api.CreateApiClient();

        (await (await client.ResetPasswordAsync("not-a-real-token", NewPassword)).ReadProblemCodeAsync(HttpStatusCode.BadRequest))
            .Should().Be("auth.invalid_reset_token");
        (await (await client.ResetPasswordAsync("not-a-real-token", "short")).ReadValidationErrorKeysAsync())
            .Should().Contain("newPassword");
    }

    [Fact]
    public async Task The_sixth_reset_request_from_one_IP_within_a_minute_gets_429()
    {
        using var client = api.CreateApiClient();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            (await client.ForgotPasswordAsync($"someone.{attempt}@nordwin.example")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        var sixth = await client.ForgotPasswordAsync("someone.6@nordwin.example");

        var problem = await sixth.ReadProblemAsync(HttpStatusCode.TooManyRequests);
        problem.Detail.Should().Contain("Too many password-reset requests");
        (await client.LoginAsync("nobody@nordwin.example", "guess")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "sign-in has a budget of its own");
    }

    private async Task<string> RequestResetTokenAsync(TestUser user)
    {
        using var client = api.CreateApiClient();
        (await client.ForgotPasswordAsync(user.Email)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        await api.DrainBackgroundWorkAsync();
        return CapturingEmailSender.ResetToken(api.Emails.SentTo(user.Email)[^1]);
    }
}
