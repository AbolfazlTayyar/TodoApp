using System.Reflection;
using System.Threading.Channels;
using TodoApp.Shared.Abstractions.Events;

namespace TodoApp.Shared.Infrastructure.Events;

/// <summary>
/// In-process queue between publishers and the <see cref="IntegrationEventDispatcher"/>.
/// Publishing returns immediately; handlers run in the background, so the HTTP request
/// that raised the event never waits for (or fails because of) other modules.
/// </summary>
internal sealed class IntegrationEventChannel
{
    private readonly Channel<IIntegrationEvent> _channel = Channel.CreateUnbounded<IIntegrationEvent>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelWriter<IIntegrationEvent> Writer => _channel.Writer;
    public ChannelReader<IIntegrationEvent> Reader => _channel.Reader;
}

/// <remarks>
/// Events live only in memory, so they are lost if the process crashes before they are handled.
/// At scale, replace this with the Outbox pattern (store events in the same DB transaction as the
/// state change) and a broker such as RabbitMQ, Kafka or Azure Service Bus. Modules won't notice,
/// because they only depend on <see cref="IEventBus"/>.
/// </remarks>
internal sealed class InMemoryEventBus(IntegrationEventChannel channel) : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent =>
        channel.Writer.WriteAsync(integrationEvent, cancellationToken).AsTask();
}

internal sealed record IntegrationEventHandlerDescriptor(Type EventType, Type HandlerType)
{
    private readonly MethodInfo _handleMethod = typeof(IIntegrationEventHandler<>)
        .MakeGenericType(EventType)
        .GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;

    public Task InvokeAsync(object handler, IIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        (Task)_handleMethod.Invoke(handler, [integrationEvent, cancellationToken])!;
}
