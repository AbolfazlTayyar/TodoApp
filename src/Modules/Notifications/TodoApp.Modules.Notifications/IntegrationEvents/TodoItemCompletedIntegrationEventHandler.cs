using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Notifications.Domain;
using TodoApp.Modules.Notifications.Infrastructure.Database;
using TodoApp.Modules.Todos.Contracts;
using TodoApp.Modules.Users.Contracts;
using TodoApp.Shared.Abstractions.Events;

namespace TodoApp.Modules.Notifications.IntegrationEvents;

internal sealed class TodoItemCompletedIntegrationEventHandler(
    NotificationsDbContext dbContext,
    IUsersModuleApi usersModuleApi,
    TimeProvider timeProvider) : IIntegrationEventHandler<TodoItemCompletedIntegrationEvent>
{
    public async Task HandleAsync(TodoItemCompletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (await dbContext.Notifications.AnyAsync(n => n.SourceEventId == integrationEvent.Id, cancellationToken))
        {
            return;
        }

        // Synchronous cross-module call through the Users public API. Never through Users' DbContext.
        var user = await usersModuleApi.GetUserAsync(integrationEvent.OwnerId, cancellationToken);

        var notification = Notification.Create(
            integrationEvent.OwnerId,
            "Task completed",
            $"Nice work, {user?.DisplayName ?? "there"}! You completed \"{integrationEvent.Title}\".",
            integrationEvent.Id,
            timeProvider.GetUtcNow().UtcDateTime);

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
