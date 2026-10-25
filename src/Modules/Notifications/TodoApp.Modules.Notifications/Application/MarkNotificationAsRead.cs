using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Notifications.Domain;
using TodoApp.Modules.Notifications.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Notifications.Application;

internal sealed record MarkNotificationAsReadCommand(Guid UserId, Guid NotificationId) : ICommand;

internal sealed class MarkNotificationAsReadCommandHandler(NotificationsDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<MarkNotificationAsReadCommand>
{
    public async Task<Result> HandleAsync(MarkNotificationAsReadCommand command, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(
            n => n.Id == command.NotificationId && n.UserId == command.UserId,
            cancellationToken);

        if (notification is null)
        {
            return NotificationErrors.NotFound;
        }

        notification.MarkAsRead(timeProvider.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
