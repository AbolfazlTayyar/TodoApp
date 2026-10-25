using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Notifications.Application.Abstractions;
using TodoApp.Modules.Notifications.Domain;
using TodoApp.Modules.Notifications.Infrastructure.Database;
using TodoApp.Modules.Users.Contracts;
using TodoApp.Shared.Abstractions.Events;

namespace TodoApp.Modules.Notifications.IntegrationEvents;

/// <summary>
/// Same event as the Todos handler, but a different module and a different reaction.
/// Users publishes once and doesn't know either of them exists.
/// </summary>
internal sealed class UserRegisteredIntegrationEventHandler(
    NotificationsDbContext dbContext,
    IEmailSender emailSender,
    TimeProvider timeProvider) : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    public async Task HandleAsync(UserRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (await dbContext.Notifications.AnyAsync(n => n.SourceEventId == integrationEvent.Id, cancellationToken))
        {
            return;
        }

        var notification = Notification.Create(
            integrationEvent.UserId,
            "Welcome!",
            $"Hi {integrationEvent.DisplayName}, your account is ready. We created an \"Inbox\" list for you.",
            integrationEvent.Id,
            timeProvider.GetUtcNow().UtcDateTime);

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        await emailSender.SendAsync(integrationEvent.Email, notification.Title, notification.Message, cancellationToken);
    }
}
