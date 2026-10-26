using Attendance.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Authorization;

/// <summary>Marks an endpoint that an account may call while it must change its password (screen 4).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowWhilePasswordChangeRequiredAttribute : Attribute;

/// <summary>
/// Part of every authorization policy: a token that carries the <c>pwd_change</c> claim may call only
/// endpoints marked <see cref="AllowWhilePasswordChangeRequiredAttribute"/>. Anonymous endpoints (sign-in,
/// refresh, sign-out) skip authorization altogether, so they stay open too. The claim is set when the
/// token is issued, so the restriction starts or ends at the next sign-in or refresh.
/// </summary>
internal sealed class PasswordChangeRequirement
    : AuthorizationHandler<PasswordChangeRequirement>, IAuthorizationRequirement
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PasswordChangeRequirement requirement)
    {
        var mustChange = context.User.HasClaim(c => c.Type == AuthClaimTypes.PasswordChangeRequired);
        var allowed = context.Resource is HttpContext http
            && http.GetEndpoint()?.Metadata.GetMetadata<AllowWhilePasswordChangeRequiredAttribute>() is not null;

        if (!mustChange || allowed)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Answers a request refused by <see cref="PasswordChangeRequirement"/> with a 403 ProblemDetails whose
/// <c>code</c> tells the client to send the user to the change-password screen. Every other outcome is
/// handled as usual.
/// </summary>
internal sealed class PasswordChangeResultHandler(IProblemDetailsService problemDetails) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var passwordChangeRequired = authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<PasswordChangeRequirement>().Any() == true;

        if (!passwordChangeRequired)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var error = AuthErrors.PasswordChangeRequired;
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = error.Message,
                Extensions = { ["code"] = error.Code },
            },
        });
    }
}
