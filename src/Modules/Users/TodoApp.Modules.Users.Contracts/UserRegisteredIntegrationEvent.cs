using TodoApp.Shared.Abstractions.Events;

namespace TodoApp.Modules.Users.Contracts;

public sealed record UserRegisteredIntegrationEvent(Guid UserId, string Email, string DisplayName) : IntegrationEvent;
