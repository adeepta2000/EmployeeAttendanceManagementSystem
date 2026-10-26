using Attendance.Domain.Identity;
using FluentAssertions;

namespace Attendance.Domain.Tests.Identity;

public sealed class UserTests
{
    [Fact]
    public void Email_is_stored_trimmed_and_lower_case()
    {
        var user = new User { Email = "  Tanvir.Hossain@Nordwin.Example ", PasswordHash = "hash" };

        user.Email.Should().Be("tanvir.hossain@nordwin.example");
    }

    [Fact]
    public void Setting_a_password_ends_the_requirement_to_change_it()
    {
        var user = new User { Email = "rafiq.islam@nordwin.example", PasswordHash = "old", MustChangePassword = true };

        user.SetPassword("new");

        user.PasswordHash.Should().Be("new");
        user.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public void Clearing_the_lockout_resets_the_failure_count()
    {
        var now = new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);
        var user = new User { Email = "rafiq.islam@nordwin.example", PasswordHash = "hash" };
        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLogin(now, maxFailedAttempts: 5, TimeSpan.FromMinutes(15));
        }

        user.ClearLockout();

        user.IsLockedOut(now).Should().BeFalse();
        user.FailedLoginCount.Should().Be(0);
    }
}
