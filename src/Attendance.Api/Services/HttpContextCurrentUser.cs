using System.Security.Claims;
using Attendance.Application.Common.Interfaces;
using Attendance.Application.Identity;

namespace Attendance.Api.Services;

internal sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string? UserId => Principal?.FindFirstValue(AuthClaimTypes.Subject);

    public IReadOnlyCollection<string> Permissions =>
        Principal?.FindAll(AuthClaimTypes.Permission).Select(c => c.Value).ToArray() ?? [];

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
}
