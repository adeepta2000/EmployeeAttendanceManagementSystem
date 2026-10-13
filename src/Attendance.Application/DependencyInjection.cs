using System.Reflection;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Identity;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Attendance.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the command handlers (each wrapped in the validation pipeline), their validators and
    /// application services. The host must also bind <see cref="AuthOptions"/> and <see cref="JwtOptions"/>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddCommandHandlers(assembly);
        services.AddScoped<AuthSessionService>();

        return services;
    }

    /// <summary>
    /// Registers every <see cref="ICommandHandler{TCommand,TResult}"/> in the assembly. Resolving the
    /// interface yields the handler wrapped in <see cref="ValidatingCommandHandler{TCommand,TResult}"/>,
    /// so validation can't be skipped.
    /// </summary>
    private static void AddCommandHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlers = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>))
                .Select(service => (Implementation: type, Service: service)));

        foreach (var (implementation, service) in handlers)
        {
            services.AddScoped(implementation);

            var decorator = typeof(ValidatingCommandHandler<,>).MakeGenericType(service.GenericTypeArguments);
            services.AddScoped(service, provider => ActivatorUtilities.CreateInstance(
                provider, decorator, provider.GetRequiredService(implementation)));
        }
    }
}
