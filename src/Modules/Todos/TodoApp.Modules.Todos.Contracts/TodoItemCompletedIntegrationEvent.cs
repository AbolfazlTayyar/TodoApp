using TodoApp.Shared.Abstractions.Events;

namespace TodoApp.Modules.Todos.Contracts;

// Carry the data consumers need (e.g. Title), so they don't have to call back into Todos.
public sealed record TodoItemCompletedIntegrationEvent(
    Guid TodoItemId,
    Guid OwnerId,
    string Title,
    DateTime CompletedAtUtc) : IntegrationEvent;
