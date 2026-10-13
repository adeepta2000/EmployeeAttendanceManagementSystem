using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Domain.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public sealed class RefreshTokenTests(AttendanceApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Refresh_issues_a_new_pair_and_revokes_the_presented_token()
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();
        var signIn = await client.SignInAsync(user);

        var refreshed = await (await client.RefreshAsync(signIn.RefreshToken)).ReadTokensAsync();

        refreshed.RefreshToken.Should().NotBe(signIn.RefreshToken);
        refreshed.AccessToken.Should().NotBe(signIn.AccessToken);
        refreshed.RefreshTokenExpiresUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));

        var old = await StoredAsync(signIn.RefreshToken);
        var successor = await StoredAsync(refreshed.RefreshToken);
        old.RevokedUtc.Should().NotBeNull();
        old.ReplacedByTokenHash.Should().Be(successor.TokenHash);
        successor.FamilyId.Should().Be(old.FamilyId);
        successor.RevokedUtc.Should().BeNull();

        var me = await client.GetWithTokenAsync("/test/probe/me", refreshed.AccessToken);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_whole_family_and_forces_a_new_login()
    {
        var user = await api.CreateUserAsync();
        using var victim = api.CreateApiClient();
        using var attacker = api.CreateApiClient();
        var stolen = await victim.SignInAsync(user);
        var otherDevice = await victim.SignInAsync(user);

        // The attacker uses the stolen token first; the victim's client is now holding a rotated one.
        var attackerTokens = await (await attacker.RefreshAsync(stolen.RefreshToken)).ReadTokensAsync();

        // The victim presents the token the attacker already rotated: reuse.
        var reuse = await victim.RefreshAsync(stolen.RefreshToken);

        var problem = await reuse.ReadProblemAsync(HttpStatusCode.Unauthorized);
        problem.Extensions["code"]!.ToString().Should().Be("auth.invalid_refresh_token");

        var familyId = (await StoredAsync(stolen.RefreshToken)).FamilyId;
        var family = await api.QueryDbAsync(db => db.RefreshTokens.Where(t => t.FamilyId == familyId).ToListAsync(Ct));
        family.Should().HaveCount(2).And.OnlyContain(t => t.RevokedUtc != null, "the whole family is revoked");

        // The attacker's fresh token is dead too, so neither party can continue without the password.
        (await attacker.RefreshAsync(attackerTokens.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Other sessions survive, and the user can sign in again.
        (await victim.RefreshAsync(otherDevice.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await victim.LoginAsync(user.Email)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_revokes_the_session()
    {
        var user = await api.CreateUserAsync();
        using var client = api.CreateApiClient();
        var signIn = await client.SignInAsync(user);
        var refreshed = await (await client.RefreshAsync(signIn.RefreshToken)).ReadTokensAsync();

        (await client.LogoutAsync(refreshed.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.RefreshAsync(refreshed.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var familyId = (await StoredAsync(signIn.RefreshToken)).FamilyId;
        (await api.QueryDbAsync(db => db.RefreshTokens.Where(t => t.FamilyId == familyId).ToListAsync(Ct)))
            .Should().OnlyContain(t => t.RevokedUtc != null);
    }

    [Fact]
    public async Task Logout_with_an_unknown_token_still_succeeds()
    {
        using var client = api.CreateApiClient();

        var response = await client.LogoutAsync("not-a-token-we-issued");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, "logout doesn't reveal whether a token exists");
    }

    private Task<RefreshToken> StoredAsync(string refreshToken)
    {
        var hash = AuthApi.Sha256Hex(refreshToken);
        return api.QueryDbAsync(db => db.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == hash, Ct));
    }
}
