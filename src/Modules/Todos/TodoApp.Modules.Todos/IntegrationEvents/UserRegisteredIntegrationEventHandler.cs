using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Todos.Domain;
using TodoApp.Modules.Todos.Infrastructure.Database;
using TodoApp.Modules.Users.Contracts;
using TodoApp.Shared.Abstractions.Events;

namespace TodoApp.Modules.Todos.IntegrationEvents;

/// <summary>Every new user gets a default "Inbox" list.</summary>
internal sealed class UserRegisteredIntegrationEventHandler(TodosDbContext dbContext, TimeProvider timeProvider)
    : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    public async Task HandleAsync(UserRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        // Idempotent: receiving the same event twice must not create a second default list.
        var alreadyHandled = await dbContext.TodoLists.AnyAsync(
            l => l.OwnerId == integrationEvent.UserId && l.IsDefault,
            cancellationToken);

        if (alreadyHandled)
        {
            return;
        }

        dbContext.TodoLists.Add(TodoList.CreateDefault(integrationEvent.UserId, timeProvider.GetUtcNow().UtcDateTime));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
