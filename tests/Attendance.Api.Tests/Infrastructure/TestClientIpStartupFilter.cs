using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Attendance.Api.Tests.Infrastructure;

/// <summary>
/// Sets the connection's remote IP from a test-only header, ahead of the whole pipeline. TestServer
/// has no client address, and sign-in is rate limited per IP, so each test picks its own.
/// </summary>
internal sealed class TestClientIpStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Client-IP";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            if (IPAddress.TryParse(context.Request.Headers[HeaderName].ToString(), out var address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            return nextMiddleware(context);
        });

        next(app);
    };
}
