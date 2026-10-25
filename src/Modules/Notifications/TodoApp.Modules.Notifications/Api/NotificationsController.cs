using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Modules.Notifications.Application;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Pagination;
using TodoApp.Shared.Infrastructure.Api;

namespace TodoApp.Modules.Notifications.Api;

[Route("api/notifications")]
internal sealed class NotificationsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<NotificationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPage(
        [FromServices] IQueryHandler<GetNotificationsQuery, PagedResult<NotificationResponse>> handler,
        [FromQuery] bool unreadOnly = false,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        OkOrProblem(await handler.HandleAsync(
            new GetNotificationsQuery(CurrentUserId, unreadOnly, page, pageSize),
            cancellationToken));

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MarkAsRead(
        Guid id,
        [FromServices] ICommandHandler<MarkNotificationAsReadCommand> handler,
        CancellationToken cancellationToken) =>
        NoContentOrProblem(await handler.HandleAsync(new MarkNotificationAsReadCommand(CurrentUserId, id), cancellationToken));
}
