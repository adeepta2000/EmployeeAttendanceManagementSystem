using System.Reflection;
using Attendance.Application.Common.Events;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Common.Security;
using Attendance.Application.Identity;
using Attendance.Application.Identity.ForgotPassword;
using Attendance.Application.Organization.Departments;
using Attendance.Application.Organization.Employees;
using Attendance.Application.Scheduling.Holidays;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Attendance.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the command and query handlers (each wrapped in the validation pipeline), their
    /// validators and application services. The host must also bind <see cref="AuthOptions"/> and
    /// <see cref="JwtOptions"/>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddHandlers(assembly, typeof(ICommandHandler<,>), typeof(ValidatingCommandHandler<,>));
        services.AddHandlers(assembly, typeof(IQueryHandler<,>), typeof(ValidatingQueryHandler<,>));
        services.AddScoped<AuthSessionService>();
        services.AddScoped<PasswordService>();
        services.AddScoped<PasswordResetLinkIssuer>();
        services.AddScoped<EmployeeScopeResolver>();
        services.AddScoped<DepartmentReferenceResolver>();
        services.AddScoped<EmployeeAssignmentResolver>();
        services.AddScoped<HolidayLocationResolver>();

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddDomainEventHandlers(assembly);

        return services;
    }

    /// <summary>Registers every domain event handler in the assembly. An event may have several; all of them run.</summary>
    private static void AddDomainEventHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlers = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                .Select(service => (Implementation: type, Service: service)));

        foreach (var (implementation, service) in handlers)
        {
            services.AddScoped(service, implementation);
        }
    }

    /// <summary>
    /// Registers every implementation of <paramref name="handlerInterface"/> in the assembly. Resolving
    /// the interface yields the handler wrapped in <paramref name="validatingDecorator"/>, so validation
    /// can't be skipped.
    /// </summary>
    private static void AddHandlers(this IServiceCollection services, Assembly assembly, Type handlerInterface, Type validatingDecorator)
    {
        var handlers = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                .Select(service => (Implementation: type, Service: service)));

        foreach (var (implementation, service) in handlers)
        {
            services.AddScoped(implementation);

            var decorator = validatingDecorator.MakeGenericType(service.GenericTypeArguments);
            services.AddScoped(service, provider => ActivatorUtilities.CreateInstance(
                provider, decorator, provider.GetRequiredService(implementation)));
        }
    }
}
