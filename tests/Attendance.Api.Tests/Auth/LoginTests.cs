using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Infrastructure.Persistence.Seeding;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Attendance.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public sealed class LoginTests(AttendanceApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Successful_login_returns_a_15_minute_access_token_and_a_7_day_refresh_token()
    {
        var user = await api.CreateUserAsync(SystemRoles.Manager);
        var clientIp = AttendanceApiFactory.NextClientIp();
        using var client = api.CreateApiClient(clientIp);

        var tokens = await (await client.LoginAsync(user.Email)).ReadTokensAsync();

        var now = DateTime.UtcNow;
        tokens.AccessTokenExpiresUtc.Should().BeCloseTo(now.AddMinutes(15), TimeSpan.FromMinutes(1));
        tokens.RefreshTokenExpiresUtc.Should().BeCloseTo(now.AddDays(7), TimeSpan.FromMinutes(1));
        tokens.MustChangePassword.Should().BeFalse();

        var jwt = new JsonWebToken(tokens.AccessToken);
        jwt.Subject.Should().Be(user.PublicId.ToString());
        jwt.ValidTo.Should().BeCloseTo(tokens.AccessTokenExpiresUtc, TimeSpan.FromSeconds(1));
        jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value)
            .Should().BeEquivalentTo(SystemRoles.All.Single(r => r.Name == SystemRoles.Manager).DefaultPermissions);
        jwt.Claims.Should().NotContain(c => c.Type == "role", "authorization is by permission, never by role");

        // Only the hash is stored, with the session metadata.
        var stored = await api.QueryDbAsync(db => db.RefreshTokens.SingleAsync(t => t.User.PublicId == user.PublicId, Ct));
        stored.TokenHash.Should().Be(AuthApi.Sha256Hex(tokens.RefreshToken)).And.NotBe(tokens.RefreshToken);
        stored.FamilyId.Should().NotBeEmpty();
        stored.CreatedByIp.Should().Be(clientIp);
        stored.UserAgent.Should().Be("Attendance.Api.Tests/1.0");
        stored.RevokedUtc.Should().BeNull();

        var signedIn = await api.QueryDbAsync(db => db.Users.SingleAsync(u => u.PublicId == user.PublicId, Ct));
        signedIn.LastLoginUtc.Should().BeCloseTo(now, TimeSpan.FromMinutes(1));

        var me = await client.GetWithTokenAsync("/test/probe/me", tokens.AccessToken);
        me.StatusCode.Should().Be(HttpStatusCode.OK, "the access token authenticates API calls");
    }

    [Fact]
    public async Task Wrong_password_returns_a_generic_401_and_counts_the_failure()
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();

        var response = await client.LoginAsync(user.Email, "not-the-password");

        var problem = await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        problem.Detail.Should().Be("Invalid credentials.");
        problem.Extensions["code"]!.ToString().Should().Be("auth.invalid_credentials");

        var stored = await api.QueryDbAsync(db => db.Users.SingleAsync(u => u.PublicId == user.PublicId, Ct));
        stored.FailedLoginCount.Should().Be(1);
        (await api.QueryDbAsync(db => db.RefreshTokens.AnyAsync(t => t.UserId == stored.Id, Ct))).Should().BeFalse();
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_response_as_a_wrong_password()
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();

        var wrongPassword = await (await client.LoginAsync(user.Email, "not-the-password")).ReadProblemAsync(HttpStatusCode.Unauthorized);
        var unknownEmail = await (await client.LoginAsync("nobody@nordwin.example", "not-the-password")).ReadProblemAsync(HttpStatusCode.Unauthorized);

        Comparable(unknownEmail).Should().BeEquivalentTo(Comparable(wrongPassword));
    }

    [Fact]
    public async Task Five_failed_attempts_lock_the_account_even_against_the_right_password()
    {
        var user = await api.CreateUserAsync();

        // Each attempt comes from a different IP, so the per-IP rate limit never applies: the lockout
        // is per account and stops guessing spread over many addresses.
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var attacker = api.CreateApiClient();
            (await attacker.LoginAsync(user.Email, $"guess-{attempt}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using var owner = api.CreateApiClient();
        var response = await owner.LoginAsync(user.Email);

        var problem = await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        problem.Detail.Should().Be("Invalid credentials.", "a locked account must not be distinguishable from a wrong password");

        var stored = await api.QueryDbAsync(db => db.Users.SingleAsync(u => u.PublicId == user.PublicId, Ct));
        stored.FailedLoginCount.Should().Be(5);
        stored.LockoutEndUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1));
        stored.LastLoginUtc.Should().BeNull();
    }

    [Fact]
    public async Task A_malformed_email_is_a_validation_error()
    {
        using var client = api.CreateApiClient();

        var response = await client.LoginAsync("not-an-email", "whatever");

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        problem.Extensions.Should().ContainKey("errors");
        problem.Extensions["errors"]!.ToString().Should().Contain("email");
    }

    private static object Comparable(ProblemDetails problem) => new
    {
        problem.Status,
        problem.Title,
        problem.Detail,
        problem.Type,
        Code = problem.Extensions["code"]!.ToString(),
    };
}
