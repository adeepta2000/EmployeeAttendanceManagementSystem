using Attendance.Application.Identity;
using Attendance.Application.Identity.Abstractions;
using Attendance.Domain.Identity;
using Attendance.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Application.Tests.Identity;

/// <summary>Refresh-token rotation and reuse detection (spec §2.5).</summary>
public sealed class RefreshTokenCommandHandlerTests : IDisposable
{
    private const string Email = "nusrat.jahan@nordwin.example";

    private readonly AuthHarness _auth = new();

    public void Dispose() => _auth.Dispose();

    [Fact]
    public async Task Refresh_rotates_the_token_within_the_same_family()
    {
        var signIn = await SignInAsync();

        var refreshed = await _auth.RefreshAsync(signIn.RefreshToken);

        refreshed.IsSuccess.Should().BeTrue();
        refreshed.Value.RefreshToken.Should().NotBe(signIn.RefreshToken);
        refreshed.Value.AccessToken.Should().NotBe(signIn.AccessToken);

        var old = await _auth.GetStoredAsync(signIn.RefreshToken);
        var successor = await _auth.GetStoredAsync(refreshed.Value.RefreshToken);
        old.RevokedUtc.Should().Be(_auth.Clock.Now.UtcDateTime);
        old.ReplacedByTokenHash.Should().Be(successor.TokenHash);
        successor.FamilyId.Should().Be(old.FamilyId);
        successor.IsActive(_auth.Clock.Now.UtcDateTime).Should().BeTrue();
        successor.CreatedByIp.Should().Be(AuthHarness.ClientIp);
        successor.UserAgent.Should().Be(AuthHarness.UserAgent);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_family_and_forces_a_new_sign_in()
    {
        var signIn = await SignInAsync();
        var second = (await _auth.RefreshAsync(signIn.RefreshToken)).Value;
        var third = (await _auth.RefreshAsync(second.RefreshToken)).Value;
        var familyId = (await _auth.GetStoredAsync(signIn.RefreshToken)).FamilyId;

        // The first token was rotated away long ago. Presenting it again is the stolen-token signal.
        var reuse = await _auth.RefreshAsync(signIn.RefreshToken);

        reuse.IsSuccess.Should().BeFalse();
        reuse.Error.Should().Be(AuthErrors.InvalidRefreshToken);

        var family = await _auth.GetFamilyAsync(familyId);
        family.Should().HaveCount(3).And.OnlyContain(t => t.IsRevoked, "reuse revokes every token in the family");

        // Even the newest token, which was never misused, is dead: the user has to sign in again.
        var latest = await _auth.RefreshAsync(third.RefreshToken);
        latest.Error.Should().Be(AuthErrors.InvalidRefreshToken);

        (await _auth.LoginAsync(Email)).IsSuccess.Should().BeTrue("a fresh sign-in starts a new family");
    }

    [Fact]
    public async Task Reuse_leaves_the_users_other_sessions_alone()
    {
        var laptop = await SignInAsync();
        var phone = (await _auth.LoginAsync(Email)).Value;
        await _auth.RefreshAsync(laptop.RefreshToken);

        await _auth.RefreshAsync(laptop.RefreshToken);

        (await _auth.RefreshAsync(phone.RefreshToken)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Presenting_one_token_twice_at_the_same_time_is_treated_as_reuse()
    {
        var signIn = await SignInAsync();
        Result<AuthTokens>? competing = null;

        // The handler reads the token, then, before it saves, a second request rotates the same token.
        // The handler's save loses the race on the RevokedUtc concurrency token.
        var racing = await _auth.RefreshAsync(
            signIn.RefreshToken,
            repository => new RaceOnFirstRead(repository, async () => competing = await _auth.RefreshAsync(signIn.RefreshToken)));

        competing!.IsSuccess.Should().BeTrue("the first request to save wins the rotation");
        racing.Error.Should().Be(AuthErrors.InvalidRefreshToken);

        var familyId = (await _auth.GetStoredAsync(signIn.RefreshToken)).FamilyId;
        (await _auth.GetFamilyAsync(familyId)).Should().HaveCount(2).And.OnlyContain(t => t.IsRevoked);
        (await _auth.RefreshAsync(competing.Value.RefreshToken)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task An_expired_token_is_rejected_without_revoking_the_family()
    {
        var signIn = await SignInAsync();
        _auth.Clock.Now += new AuthOptions().RefreshTokenLifetime;

        var result = await _auth.RefreshAsync(signIn.RefreshToken);

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
        (await _auth.GetStoredAsync(signIn.RefreshToken)).IsRevoked.Should().BeFalse("expiry is not a theft signal");
    }

    [Fact]
    public async Task An_unknown_token_is_rejected()
    {
        var result = await _auth.RefreshAsync(_auth.TokenGenerator.Generate().Token);

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task A_deactivated_users_session_ends_at_the_next_refresh()
    {
        var signIn = await SignInAsync();
        await using (var context = _auth.CreateContext())
        {
            (await context.Users.SingleAsync(TestContext.Current.CancellationToken)).IsActive = false;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await _auth.RefreshAsync(signIn.RefreshToken);

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
        (await _auth.GetStoredAsync(signIn.RefreshToken)).IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_revokes_the_family_of_the_presented_token()
    {
        var signIn = await SignInAsync();
        var refreshed = (await _auth.RefreshAsync(signIn.RefreshToken)).Value;

        (await _auth.LogoutAsync(refreshed.RefreshToken)).IsSuccess.Should().BeTrue();

        var familyId = (await _auth.GetStoredAsync(signIn.RefreshToken)).FamilyId;
        (await _auth.GetFamilyAsync(familyId)).Should().OnlyContain(t => t.IsRevoked);
        (await _auth.RefreshAsync(refreshed.RefreshToken)).IsSuccess.Should().BeFalse();
    }

    private async Task<AuthTokens> SignInAsync()
    {
        await _auth.CreateUserAsync(Email);
        var result = await _auth.LoginAsync(Email);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    /// <summary>Runs a competing request right after the handler's first token lookup.</summary>
    private sealed class RaceOnFirstRead(IRefreshTokenRepository inner, Func<Task> competingRequest)
        : IRefreshTokenRepository
    {
        private bool _raced;

        public async Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
        {
            var token = await inner.FindByHashAsync(tokenHash, cancellationToken);
            if (!_raced)
            {
                _raced = true;
                await competingRequest();
            }

            return token;
        }

        public Task<IReadOnlyList<RefreshToken>> GetUnrevokedInFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
            inner.GetUnrevokedInFamilyAsync(familyId, cancellationToken);

        public Task<IReadOnlyList<RefreshToken>> GetUnrevokedForUserAsync(long userId, CancellationToken cancellationToken) =>
            inner.GetUnrevokedForUserAsync(userId, cancellationToken);

        public void Add(RefreshToken token) => inner.Add(token);
    }
}
