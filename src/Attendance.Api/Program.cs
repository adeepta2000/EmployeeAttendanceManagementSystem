using System.Text.Json.Serialization;
using Attendance.Api.Middleware;
using Attendance.Api.Services;
using Attendance.Api.Setup;
using Attendance.Application;
using Attendance.Application.Common.Interfaces;
using Attendance.Infrastructure;
using Attendance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Levels come from the "Serilog" configuration section. Development gets readable console lines;
// everywhere else gets one JSON object per event for a log collector.
builder.Services.AddSerilog((services, logger) =>
{
    logger.ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();

    if (builder.Environment.IsDevelopment())
    {
        logger.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}");
    }
    else
    {
        logger.WriteTo.Console(new RenderedCompactJsonFormatter());
    }
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Enums travel as their names ("Permanent", not 3). Numbers are refused, so a client can't send a
// value that isn't a defined member.
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Trust X-Forwarded-For only from loopback proxies (the default) so the client IP used for rate
// limiting and session metadata can't be spoofed. Add the BFF's address to KnownProxies when deployed.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

builder.Services.AddJwtAuthentication();
builder.Services.AddPermissionAuthorization();
builder.Services.AddAuthRateLimiting();
builder.Services.AddSwaggerWithJwt();

var app = builder.Build();

// Startup guard: apply pending migrations and seed reference data before serving any request.
await app.Services.InitializeDatabaseAsync(app.Lifetime.ApplicationStopping);

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = "Nordwin Attendance API");
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Liveness runs no checks: it only shows the process is up. Readiness also checks the database.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(Attendance.Infrastructure.DependencyInjection.ReadinessTag),
}).AllowAnonymous();

app.Run();
