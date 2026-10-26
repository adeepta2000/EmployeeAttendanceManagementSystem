using Attendance.Domain.Identity;
using FluentAssertions;

namespace Attendance.Domain.Tests.Identity;

public sealed class PasswordResetTokenTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_new_link_is_usable_until_it_expires()
    {
        var link = NewLink();

        link.IsUsable(Now).Should().BeTrue();
        link.IsUsable(Now.AddMinutes(30)).Should().BeFalse("a link is expired from ExpiresUtc onwards");
    }

    [Fact]
    public void A_used_link_is_not_usable_and_stays_recorded_as_used()
    {
        var link = NewLink();

        link.MarkUsed(Now);
        link.Revoke(Now.AddMinutes(1));

        link.IsUsable(Now).Should().BeFalse();
        link.UsedUtc.Should().Be(Now);
        link.RevokedUtc.Should().BeNull("revoking a used link changes nothing");
    }

    [Fact]
    public void A_revoked_link_can_no_longer_be_used()
    {
        var link = NewLink();

        link.Revoke(Now);
        link.MarkUsed(Now.AddMinutes(1));

        link.IsUsable(Now).Should().BeFalse();
        link.RevokedUtc.Should().Be(Now);
        link.UsedUtc.Should().BeNull();
    }

    private static PasswordResetToken NewLink() => new()
    {
        TokenHash = "HASH",
        ExpiresUtc = Now.AddMinutes(30),
        CreatedByIp = "10.0.0.1",
    };
}
