namespace TodoApp.Shared.Abstractions.Events;

/// <summary>
/// A fact that one module publishes for other modules to react to ("a user registered").
/// The publisher doesn't know who listens, so modules stay decoupled.
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Handlers must be idempotent: events are delivered "at least once" (the outbox retries failed deliveries,
/// and re-runs every handler of an event when one of them fails), so the same event can arrive twice.
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
