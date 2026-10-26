using Microsoft.Extensions.DependencyInjection;
using TodoApp.Shared.Infrastructure.Api;
using TodoApp.Shared.Infrastructure.Auth;
using TodoApp.Shared.Infrastructure.Events;
using TodoApp.Shared.Infrastructure.Outbox;

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

        services.AddSingleton<IntegrationEventDispatcher>();
        services.AddSingleton<OutboxSignal>();
        services.AddSingleton<OutboxSignalInterceptor>();

        return services;
    }

    /// <summary>
    /// Starts the single background service that delivers every module's outbox. Call it AFTER registering
    /// the modules, so it starts after their database migrators (hosted services start in registration order).
    /// </summary>
    public static IServiceCollection AddOutboxProcessor(this IServiceCollection services) =>
        services.AddHostedService<OutboxProcessor>();
}
