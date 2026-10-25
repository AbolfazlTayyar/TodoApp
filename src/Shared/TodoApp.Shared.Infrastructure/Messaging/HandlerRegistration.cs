using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using TodoApp.Shared.Abstractions.Events;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Infrastructure.Events;

namespace TodoApp.Shared.Infrastructure.Messaging;

public static class HandlerRegistration
{
    private static readonly Type[] RequestHandlerTypes =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>)
    ];

    /// <summary>
    /// Registers every command, query and integration event handler found in a module's assembly,
    /// so adding a use case never requires touching DI registration code.
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var concreteTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var type in concreteTypes)
        {
            foreach (var @interface in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var definition = @interface.GetGenericTypeDefinition();

                if (RequestHandlerTypes.Contains(definition))
                {
                    services.AddScoped(@interface, type);
                }
                else if (definition == typeof(IIntegrationEventHandler<>))
                {
                    services.AddScoped(type);
                    services.AddSingleton(new IntegrationEventHandlerDescriptor(@interface.GetGenericArguments()[0], type));
                }
            }
        }

        return services;
    }
}
