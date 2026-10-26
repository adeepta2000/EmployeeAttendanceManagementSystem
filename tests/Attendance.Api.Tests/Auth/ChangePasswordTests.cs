using System.Net;
using System.Net.Http.Json;
using Attendance.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public sealed class ChangePasswordTests(AttendanceApiFactory api)
{
    private const string NewPassword = "Brand-New-Pa55phrase";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Changing_the_password_starts_a_new_session_and_ends_every_other_one()
    {
        var user = await api.CreateUserAsync();
        using var laptop = api.CreateApiClient();
        using var phone = api.CreateApiClient();
        var laptopSession = await laptop.SignInAsync(user);
        var phoneSession = await phone.SignInAsync(user);

        var response = await laptop.ChangePasswordAsync(laptopSession.AccessToken, AttendanceApiFactory.Password, NewPassword);

        var fresh = await response.ReadTokensAsync();
        fresh.MustChangePassword.Should().BeFalse();
        (await laptop.RefreshAsync(fresh.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.OK, "the response starts a new session");

        // Both earlier sessions are over, the caller's own included.
        (await laptop.RefreshAsync(laptopSession.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await phone.RefreshAsync(phoneSession.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await laptop.LoginAsync(user.Email)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the old password no longer works");
        (await laptop.LoginAsync(user.Email, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_wrong_current_password_is_a_400_that_counts_towards_the_lockout()
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();
        var session = await client.SignInAsync(user);

        var response = await client.ChangePasswordAsync(session.AccessToken, "not-the-password", NewPassword);

        // 400, not 401: the session is fine, and a 401 would make the client sign the user out.
        (await response.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("auth.invalid_current_password");
        var stored = await api.QueryDbAsync(db => db.Users.SingleAsync(u => u.PublicId == user.PublicId, Ct));
        stored.FailedLoginCount.Should().Be(1);
        (await client.RefreshAsync(session.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.OK, "the session survives");
    }

    [Fact]
    public async Task Five_wrong_current_passwords_lock_the_account()
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();
        var session = await client.SignInAsync(user);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            (await (await client.ChangePasswordAsync(session.AccessToken, $"guess-{attempt}", NewPassword))
                .ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("auth.invalid_current_password");
        }

        (await (await client.ChangePasswordAsync(session.AccessToken, "guess-5", NewPassword))
            .ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("auth.locked_out");

        // Locked: even the right current password is refused, and so is signing in.
        (await (await client.ChangePasswordAsync(session.AccessToken, AttendanceApiFactory.Password, NewPassword))
            .ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("auth.locked_out");
        (await client.LoginAsync(user.Email)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("short-pw-1")]
    [InlineData(AttendanceApiFactory.Password)]
    public async Task A_new_password_that_is_too_short_or_unchanged_is_refused(string newPassword)
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();
        var session = await client.SignInAsync(user);

        var response = await client.ChangePasswordAsync(session.AccessToken, AttendanceApiFactory.Password, newPassword);

        (await response.ReadValidationErrorKeysAsync()).Should().Contain("newPassword");
        (await client.LoginAsync(user.Email)).StatusCode.Should().Be(HttpStatusCode.OK, "the password is unchanged");
    }

    [Fact]
    public async Task Changing_the_password_needs_a_signed_in_user()
    {
        using var client = api.CreateApiClient();

        var response = await client.PostAsync("/api/auth/change-password", JsonBody(), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static HttpContent JsonBody() => JsonContent.Create(
        new { currentPassword = AttendanceApiFactory.Password, newPassword = NewPassword });
}
