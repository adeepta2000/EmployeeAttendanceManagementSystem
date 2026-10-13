using Attendance.Application.Common.Interfaces;
using Attendance.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Tests.Infrastructure;

/// <summary>
/// Test-only endpoints, added to the app by <see cref="AttendanceApiFactory"/>. No business endpoint
/// exists yet, so these exercise the authorization setup through the real pipeline.
/// </summary>
[ApiController]
[Route("test/probe")]
public sealed class ProbeController(IUserContextService userContext) : ControllerBase
{
    /// <summary>Needs only a signed-in user (the fallback policy).</summary>
    [HttpGet("me")]
    public async Task<ProbeIdentity> Me(CancellationToken cancellationToken)
    {
        var current = await userContext.GetCurrentAsync(cancellationToken);
        return new ProbeIdentity(current.UserPublicId, [.. current.Permissions.Order(StringComparer.Ordinal)]);
    }

    [HttpGet("leave-approve")]
    [Authorize(Policy = Permissions.Leave.Approve)]
    public IActionResult LeaveApprove() => NoContent();
}

public sealed record ProbeIdentity(Guid UserId, string[] Permissions);
