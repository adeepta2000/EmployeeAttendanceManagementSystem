using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Infrastructure.Persistence.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Attendance.Api.Tests.Authorization;

/// <summary>Screen 4: an account that must change its password can do nothing else until it has.</summary>
[Collection(ApiCollection.Name)]
public sealed class PasswordChangeRequiredTests(AttendanceApiFactory api)
{
    private const string NewPassword = "Chosen-By-Me-Pa55phrase";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Until_the_password_is_changed_every_endpoint_but_change_password_is_forbidden()
    {
        var user = await api.CreateUserAsync(SystemRoles.Manager, mustChangePassword: true);
        using var client = api.CreateApiClient();

        var tokens = await client.SignInAsync(user);

        tokens.MustChangePassword.Should().BeTrue();
        new JsonWebToken(tokens.AccessToken).Claims.Should().Contain(c => c.Type == "pwd_change" && c.Value == "true");

        // A permission policy, the fallback policy and a real endpoint all refuse, with a code the client acts on.
        foreach (var path in new[] { "/test/probe/leave-approve", "/test/probe/me", "/api/departments" })
        {
            var response = await client.GetWithTokenAsync(path, tokens.AccessToken);
            (await response.ReadProblemCodeAsync(HttpStatusCode.Forbidden)).Should().Be("auth.password_change_required", path);
        }

        // A refresh doesn't get around it: the new token carries the claim too.
        var refreshed = await (await client.RefreshAsync(tokens.RefreshToken)).ReadTokensAsync();
        (await client.GetWithTokenAsync("/test/probe/me", refreshed.AccessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var changed = await (await client.ChangePasswordAsync(refreshed.AccessToken, AttendanceApiFactory.Password, NewPassword))
            .ReadTokensAsync();

        changed.MustChangePassword.Should().BeFalse();
        new JsonWebToken(changed.AccessToken).Claims.Should().NotContain(c => c.Type == "pwd_change");
        (await client.GetWithTokenAsync("/test/probe/leave-approve", changed.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetWithTokenAsync("/api/departments", changed.AccessToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await api.QueryDbAsync(db => db.Users.SingleAsync(u => u.PublicId == user.PublicId, Ct));
        stored.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task A_password_reset_also_clears_the_requirement()
    {
        var user = await api.CreateUserAsync(mustChangePassword: true);
        using var client = api.CreateApiClient();

        (await client.ForgotPasswordAsync(user.Email)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        await api.DrainBackgroundWorkAsync();
        var token = CapturingEmailSender.ResetToken(api.Emails.SentTo(user.Email)[^1]);
        (await client.ResetPasswordAsync(token, NewPassword)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var tokens = await (await client.LoginAsync(user.Email, NewPassword)).ReadTokensAsync();
        tokens.MustChangePassword.Should().BeFalse();
        (await client.GetWithTokenAsync("/test/probe/me", tokens.AccessToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_ordinary_token_has_no_password_change_claim()
    {
        var user = await api.CreateUserAsync(SystemRoles.Employee);
        using var client = api.CreateApiClient();

        var tokens = await client.SignInAsync(user);

        new JsonWebToken(tokens.AccessToken).Claims.Should().NotContain(c => c.Type == "pwd_change");
        (await client.GetWithTokenAsync("/test/probe/me", tokens.AccessToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
