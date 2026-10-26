using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TodoApp.Shared.Abstractions.Events;

namespace TodoApp.Shared.Infrastructure.Events;

/// <summary>
/// Runs every handler registered for an event. Called by the outbox processor, so a handler only ever
/// sees events whose originating transaction was committed.
/// </summary>
internal sealed partial class IntegrationEventDispatcher(
    IEnumerable<IntegrationEventHandlerDescriptor> descriptors,
    IServiceScopeFactory scopeFactory,
    ILogger<IntegrationEventDispatcher> logger)
{
    private readonly ILookup<Type, IntegrationEventHandlerDescriptor> _handlersByEventType =
        descriptors.ToLookup(d => d.EventType);

    // Only events that have at least one handler can be resolved; the rest have nobody to deliver to.
    private readonly Dictionary<string, Type> _eventTypesByName =
        descriptors.Select(d => d.EventType).Distinct().ToDictionary(t => t.FullName!);

    public bool TryResolveEventType(string name, [MaybeNullWhen(false)] out Type eventType) =>
        _eventTypesByName.TryGetValue(name, out eventType);

    /// <summary>Runs all handlers, even if one fails, and returns the names of the ones that failed.</summary>
    public async Task<IReadOnlyList<string>> DispatchAsync(
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        List<string> failedHandlers = [];

        foreach (var descriptor in _handlersByEventType[integrationEvent.GetType()])
        {
            if (!await TryHandleAsync(descriptor, integrationEvent, cancellationToken))
            {
                failedHandlers.Add(descriptor.HandlerType.Name);
            }
        }

        return failedHandlers;
    }

    private async Task<bool> TryHandleAsync(
        IntegrationEventHandlerDescriptor descriptor,
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        // A fresh DI scope per handler: each one gets its own DbContext,
        // and a failure in one module's handler can't affect another module.
        await using var scope = scopeFactory.CreateAsyncScope();

        try
        {
            var handler = scope.ServiceProvider.GetRequiredService(descriptor.HandlerType);
            await descriptor.InvokeAsync(handler, integrationEvent, cancellationToken);

            LogHandled(logger, integrationEvent.GetType().Name, integrationEvent.Id, descriptor.HandlerType.FullName);
            return true;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The outbox processor retries the whole event later, so handlers must be idempotent.
            LogHandlerFailed(logger, ex, integrationEvent.GetType().Name, integrationEvent.Id, descriptor.HandlerType.FullName);
            return false;
        }
    }

    // Source-generated logging: no boxing or string formatting when the level is disabled.
    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {EventType} {EventId} with {Handler}")]
    private static partial void LogHandled(ILogger logger, string eventType, Guid eventId, string? handler);

    [LoggerMessage(Level = LogLevel.Error, Message = "Handler {Handler} failed for {EventType} {EventId}")]
    private static partial void LogHandlerFailed(ILogger logger, Exception exception, string eventType, Guid eventId, string? handler);
}

internal sealed record IntegrationEventHandlerDescriptor(Type EventType, Type HandlerType)
{
    private readonly MethodInfo _handleMethod = typeof(IIntegrationEventHandler<>)
        .MakeGenericType(EventType)
        .GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;

    public Task InvokeAsync(object handler, IIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        (Task)_handleMethod.Invoke(handler, [integrationEvent, cancellationToken])!;
}
