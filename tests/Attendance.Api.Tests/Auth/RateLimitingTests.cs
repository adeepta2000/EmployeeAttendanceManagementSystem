using System.Net;
using Attendance.Api.Tests.Infrastructure;
using FluentAssertions;

namespace Attendance.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public sealed class RateLimitingTests(AttendanceApiFactory api)
{
    [Fact]
    public async Task The_sixth_login_from_one_IP_within_a_minute_gets_429()
    {
        using var client = api.CreateApiClient();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            (await client.LoginAsync("nobody@nordwin.example", "guess")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var sixth = await client.LoginAsync("nobody@nordwin.example", "guess");

        var problem = await sixth.ReadProblemAsync(HttpStatusCode.TooManyRequests);
        problem.Detail.Should().Contain("Too many sign-in attempts");

        using var otherClient = api.CreateApiClient();
        (await otherClient.LoginAsync("nobody@nordwin.example", "guess"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the limit is per IP address");
    }
}
