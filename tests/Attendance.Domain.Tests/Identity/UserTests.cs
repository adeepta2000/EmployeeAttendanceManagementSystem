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
}
