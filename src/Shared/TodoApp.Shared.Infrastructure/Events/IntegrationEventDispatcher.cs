using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TodoApp.Shared.Abstractions.Events;

namespace TodoApp.Shared.Infrastructure.Events;

internal sealed partial class IntegrationEventDispatcher(
    IntegrationEventChannel channel,
    IEnumerable<IntegrationEventHandlerDescriptor> descriptors,
    IServiceScopeFactory scopeFactory,
    ILogger<IntegrationEventDispatcher> logger) : BackgroundService
{
    private readonly ILookup<Type, IntegrationEventHandlerDescriptor> _handlersByEventType =
        descriptors.ToLookup(d => d.EventType);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var integrationEvent in channel.Reader.ReadAllAsync(stoppingToken))
        {
            foreach (var descriptor in _handlersByEventType[integrationEvent.GetType()])
            {
                await DispatchAsync(descriptor, integrationEvent, stoppingToken);
            }
        }
    }

    private async Task DispatchAsync(
        IntegrationEventHandlerDescriptor descriptor,
        IIntegrationEvent integrationEvent,
        CancellationToken stoppingToken)
    {
        // A fresh DI scope per handler: each one gets its own DbContext,
        // and a failure in one module's handler can't affect another module.
        await using var scope = scopeFactory.CreateAsyncScope();

        try
        {
            var handler = scope.ServiceProvider.GetRequiredService(descriptor.HandlerType);
            await descriptor.InvokeAsync(handler, integrationEvent, stoppingToken);

            LogHandled(logger, integrationEvent.GetType().Name, integrationEvent.Id, descriptor.HandlerType.FullName);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // A real broker would retry and eventually move the message to a dead-letter queue.
            LogHandlerFailed(logger, ex, integrationEvent.GetType().Name, integrationEvent.Id, descriptor.HandlerType.FullName);
        }
    }

    // Source-generated logging: no boxing or string formatting when the level is disabled.
    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {EventType} {EventId} with {Handler}")]
    private static partial void LogHandled(ILogger logger, string eventType, Guid eventId, string? handler);

    [LoggerMessage(Level = LogLevel.Error, Message = "Handler {Handler} failed for {EventType} {EventId}")]
    private static partial void LogHandlerFailed(ILogger logger, Exception exception, string eventType, Guid eventId, string? handler);
}
