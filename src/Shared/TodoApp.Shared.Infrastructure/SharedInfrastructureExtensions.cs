using Microsoft.Extensions.DependencyInjection;
using TodoApp.Shared.Abstractions.Events;
using TodoApp.Shared.Infrastructure.Api;
using TodoApp.Shared.Infrastructure.Auth;
using TodoApp.Shared.Infrastructure.Events;

namespace TodoApp.Shared.Infrastructure;

public static class SharedInfrastructureExtensions
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
    {
        // Inject TimeProvider instead of calling DateTime.UtcNow, so time can be faked in tests.
        services.AddSingleton(TimeProvider.System);

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddJwtAuthentication();

        services.AddSingleton<IntegrationEventChannel>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddHostedService<IntegrationEventDispatcher>();

        return services;
    }
}
