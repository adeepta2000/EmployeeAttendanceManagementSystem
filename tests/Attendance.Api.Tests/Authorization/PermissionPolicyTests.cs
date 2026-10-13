using System.Net;
using System.Net.Http.Json;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Domain.Identity;
using Attendance.Infrastructure.Persistence.Seeding;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Attendance.Api.Tests.Authorization;

[Collection(ApiCollection.Name)]
public sealed class PermissionPolicyTests(AttendanceApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_permission_code_has_a_policy_that_requires_that_permission_claim()
    {
        var policies = api.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var permission in Permissions.All)
        {
            var policy = await policies.GetPolicyAsync(permission.Code);

            policy.Should().NotBeNull(permission.Code);
            policy!.Requirements.OfType<ClaimsAuthorizationRequirement>().Should().ContainSingle(r =>
                r.ClaimType == "permission" && r.AllowedValues!.SequenceEqual(new[] { permission.Code }));
        }
    }

    [Fact]
    public async Task A_policy_protected_endpoint_is_forbidden_without_the_permission()
    {
        using var client = api.CreateApiClient();
        var tokens = await client.SignInAsync(await api.CreateUserAsync(SystemRoles.Employee));

        var response = await client.GetWithTokenAsync("/test/probe/leave-approve", tokens.AccessToken);

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_policy_protected_endpoint_allows_a_user_with_the_permission()
    {
        using var client = api.CreateApiClient();
        var tokens = await client.SignInAsync(await api.CreateUserAsync(SystemRoles.Manager));

        var response = await client.GetWithTokenAsync("/test/probe/leave-approve", tokens.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Endpoints_need_a_signed_in_user_by_default()
    {
        using var client = api.CreateApiClient();

        var anonymous = await client.GetAsync("/test/probe/me", Ct);
        var forged = await client.GetWithTokenAsync("/test/probe/me", "not.a.jwt");

        await anonymous.ReadProblemAsync(HttpStatusCode.Unauthorized);
        await forged.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_user_context_resolves_the_caller_and_their_permissions_from_the_token()
    {
        using var client = api.CreateApiClient();
        var user = await api.CreateUserAsync(SystemRoles.HRManager);
        var tokens = await client.SignInAsync(user);

        var me = await (await client.GetWithTokenAsync("/test/probe/me", tokens.AccessToken))
            .Content.ReadFromJsonAsync<ProbeIdentity>(Ct);

        me!.UserId.Should().Be(user.PublicId);
        me.Permissions.Should().BeEquivalentTo(SystemRoles.All.Single(r => r.Name == SystemRoles.HRManager).DefaultPermissions);
    }
}
