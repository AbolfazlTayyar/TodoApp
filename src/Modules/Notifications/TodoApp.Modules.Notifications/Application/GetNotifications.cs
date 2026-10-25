using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Notifications.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Pagination;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Notifications.Application;

internal sealed record GetNotificationsQuery(Guid UserId, bool UnreadOnly, int Page, int PageSize)
    : IQuery<PagedResult<NotificationResponse>>;

internal sealed record NotificationResponse(
    Guid Id,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

internal sealed class GetNotificationsQueryHandler(NotificationsDbContext dbContext)
    : IQueryHandler<GetNotificationsQuery, PagedResult<NotificationResponse>>
{
    public async Task<Result<PagedResult<NotificationResponse>>> HandleAsync(
        GetNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        var notifications = dbContext.Notifications.Where(n => n.UserId == query.UserId);

        if (query.UnreadOnly)
        {
            notifications = notifications.Where(n => n.ReadAtUtc == null);
        }

        var totalCount = await notifications.CountAsync(cancellationToken);

        // Newest first, with the unique Id as a tie-breaker so pages are stable. Don't order by the GUID alone:
        // SQL Server sorts uniqueidentifier starting from its last bytes, so GUID v7 order != creation order there.
        var page = await notifications
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => new NotificationResponse(n.Id, n.Title, n.Message, n.ReadAtUtc != null, n.CreatedAtUtc, n.ReadAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationResponse>(page, query.Page, query.PageSize, totalCount);
    }
}
