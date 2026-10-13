using Microsoft.OpenApi;

namespace Attendance.Api.Setup;

internal static class SwaggerSetup
{
    private const string BearerScheme = "Bearer";

    /// <summary>Swagger UI with an Authorize button that sends a JWT as a bearer token.</summary>
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Nordwin Attendance API",
                Version = "v1",
                Description = "Attendance, leave and overtime management for Nordwin Textiles Ltd.",
            });

            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the accessToken from POST /api/auth/login (without the \"Bearer \" prefix).",
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [],
            });
        });

        return services;
    }
}
