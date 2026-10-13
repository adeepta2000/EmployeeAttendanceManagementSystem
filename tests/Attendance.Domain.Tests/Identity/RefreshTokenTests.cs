using Attendance.Domain.Identity;
using FluentAssertions;

namespace Attendance.Domain.Tests.Identity;

public sealed class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_new_token_is_active_until_it_expires()
    {
        var token = NewToken(expiresUtc: Now.AddDays(7));

        token.IsActive(Now).Should().BeTrue();
        token.IsActive(Now.AddDays(7)).Should().BeFalse("a token is expired from ExpiresUtc onwards");
        token.IsExpired(Now.AddDays(7)).Should().BeTrue();
    }

    [Fact]
    public void Revoking_records_the_time_and_the_successor()
    {
        var token = NewToken(expiresUtc: Now.AddDays(7));

        token.Revoke(Now, replacedByTokenHash: "SUCCESSOR");

        token.IsRevoked.Should().BeTrue();
        token.IsActive(Now).Should().BeFalse();
        token.RevokedUtc.Should().Be(Now);
        token.ReplacedByTokenHash.Should().Be("SUCCESSOR");
    }

    [Fact]
    public void Revoking_again_keeps_the_original_revocation()
    {
        var token = NewToken(expiresUtc: Now.AddDays(7));
        token.Revoke(Now, replacedByTokenHash: "SUCCESSOR");

        token.Revoke(Now.AddMinutes(5));

        token.RevokedUtc.Should().Be(Now);
        token.ReplacedByTokenHash.Should().Be("SUCCESSOR", "the rotation chain must survive a later family revocation");
    }

    private static RefreshToken NewToken(DateTime expiresUtc) => new()
    {
        TokenHash = "HASH",
        FamilyId = Guid.NewGuid(),
        ExpiresUtc = expiresUtc,
        CreatedByIp = "10.0.0.1",
    };
}
