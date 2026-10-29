using Attendance.Application.Common.Files;
using Attendance.Application.Common.Interfaces;
using Attendance.Application.Identity.Abstractions;
using Attendance.Application.Organization.Abstractions;
using Attendance.Application.Scheduling.Abstractions;
using Attendance.Domain.Organization;
using Attendance.Domain.Scheduling;
using Attendance.Infrastructure.Background;
using Attendance.Infrastructure.Email;
using Attendance.Infrastructure.Files;
using Attendance.Infrastructure.Identity;
using Attendance.Infrastructure.Persistence;
using Attendance.Infrastructure.Persistence.Interceptors;
using Attendance.Infrastructure.Persistence.Repositories;
using Attendance.Infrastructure.Persistence.Seeding;
using Attendance.Infrastructure.Persistence.Seeding.Demo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Attendance.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Health-check tag for the checks behind <c>/health/ready</c>.</summary>
    public const string ReadinessTag = "ready";

    /// <summary>
    /// Registers persistence, repositories and the identity services. The host must also register an
    /// <c>ICurrentUser</c> implementation and bind <c>AuthOptions</c> and <c>JwtOptions</c>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Attendance")
            ?? throw new InvalidOperationException("Connection string 'Attendance' is not configured.");

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AttendanceDbContext>((serviceProvider, options) => options
            .UseSqlServer(connectionString)
            .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<IdentitySeeder>();

        services.AddOptions<DemoDataOptions>()
            .Bind(configuration.GetSection(DemoDataOptions.SectionName))
            .Validate(o => o.IsValid, DemoDataOptions.ValidationMessage);
        services.AddScoped<DemoDataSeeder>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IUserContextService, UserContextService>();

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(o => o.IsValid, EmailOptions.ValidationMessage)
            .ValidateOnStart();
        services.AddSingleton<IEmailSender, PickupDirectoryEmailSender>();

        services.AddSingleton<BackgroundWorkQueue>();
        services.AddSingleton<IBackgroundWorkQueue>(provider => provider.GetRequiredService<BackgroundWorkQueue>());
        services.AddHostedService<BackgroundWorkService>();

        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IMasterDataRepository<Designation>, MasterDataRepository<Designation>>();
        services.AddScoped<IMasterDataRepository<WorkLocation>, MasterDataRepository<WorkLocation>>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddSingleton<ISpreadsheetReader, ClosedXmlSpreadsheetReader>();

        services.AddScoped<IMasterDataRepository<Shift>, MasterDataRepository<Shift>>();
        services.AddScoped<IWeeklyOffPatternRepository, WeeklyOffPatternRepository>();
        services.AddScoped<IRosterRepository, RosterRepository>();
        services.AddScoped<IHolidayRepository, HolidayRepository>();
        services.AddScoped<ICalendarRepository, CalendarRepository>();

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddSingleton<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();

        services.AddHealthChecks().AddDbContextCheck<AttendanceDbContext>("database", tags: [ReadinessTag]);

        return services;
    }
}
