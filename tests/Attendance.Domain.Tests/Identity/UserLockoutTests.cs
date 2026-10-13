using Attendance.Domain.Identity;
using FluentAssertions;

namespace Attendance.Domain.Tests.Identity;

public sealed class UserLockoutTests
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly DateTime Now = new(2026, 10, 11, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Failures_below_the_limit_count_but_do_not_lock()
    {
        var user = NewUser();

        for (var i = 0; i < MaxAttempts - 1; i++)
        {
            user.RegisterFailedLogin(Now, MaxAttempts, LockoutDuration).Should().BeFalse();
        }

        user.FailedLoginCount.Should().Be(MaxAttempts - 1);
        user.IsLockedOut(Now).Should().BeFalse();
    }

    [Fact]
    public void The_fifth_failure_locks_the_account_for_the_lockout_duration()
    {
        var user = NewUser();
        FailTimes(user, MaxAttempts - 1);

        var lockedOut = user.RegisterFailedLogin(Now, MaxAttempts, LockoutDuration);

        lockedOut.Should().BeTrue();
        user.LockoutEndUtc.Should().Be(Now + LockoutDuration);
        user.IsLockedOut(Now).Should().BeTrue();
        user.IsLockedOut(Now + LockoutDuration - TimeSpan.FromSeconds(1)).Should().BeTrue();
        user.IsLockedOut(Now + LockoutDuration).Should().BeFalse("the lockout ends exactly at LockoutEndUtc");
    }

    [Fact]
    public void A_failure_after_the_lockout_expires_starts_a_fresh_count()
    {
        var user = NewUser();
        FailTimes(user, MaxAttempts);
        var afterLockout = Now + LockoutDuration + TimeSpan.FromMinutes(1);

        var lockedOut = user.RegisterFailedLogin(afterLockout, MaxAttempts, LockoutDuration);

        lockedOut.Should().BeFalse();
        user.FailedLoginCount.Should().Be(1);
        user.LockoutEndUtc.Should().BeNull();
    }

    [Fact]
    public void A_successful_sign_in_clears_the_failures_and_records_the_time()
    {
        var user = NewUser();
        FailTimes(user, MaxAttempts - 1);

        user.RegisterSuccessfulLogin(Now);

        user.FailedLoginCount.Should().Be(0);
        user.LockoutEndUtc.Should().BeNull();
        user.LastLoginUtc.Should().Be(Now);
    }

    private static void FailTimes(User user, int times)
    {
        for (var i = 0; i < times; i++)
        {
            user.RegisterFailedLogin(Now, MaxAttempts, LockoutDuration);
        }
    }

    private static User NewUser() => new() { Email = "rafiq.islam@nordwin.example", PasswordHash = "hash" };
}
