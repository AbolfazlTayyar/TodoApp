using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Todos.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Pagination;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Application.TodoItems;

internal sealed record GetTodoItemsQuery(
    Guid OwnerId,
    Guid? ListId,
    bool? IsCompleted,
    int Page,
    int PageSize) : IQuery<PagedResult<TodoItemResponse>>;

internal sealed class GetTodoItemsQueryHandler(TodosDbContext dbContext)
    : IQueryHandler<GetTodoItemsQuery, PagedResult<TodoItemResponse>>
{
    public async Task<Result<PagedResult<TodoItemResponse>>> HandleAsync(
        GetTodoItemsQuery query,
        CancellationToken cancellationToken)
    {
        var items = dbContext.TodoItems.Where(i => i.OwnerId == query.OwnerId);

        if (query.ListId is { } listId)
        {
            items = items.Where(i => i.ListId == listId);
        }

        if (query.IsCompleted is { } isCompleted)
        {
            items = items.Where(i => i.IsCompleted == isCompleted);
        }

        var totalCount = await items.CountAsync(cancellationToken);

        // Paging needs a deterministic order, so the unique Id is the final tie-breaker.
        // For very deep pages, keyset ("WHERE Id > lastSeenId") pagination beats Skip/Take.
        var page = await items
            .OrderBy(i => i.IsCompleted)
            .ThenBy(i => i.DueDateUtc == null)
            .ThenBy(i => i.DueDateUtc)
            .ThenByDescending(i => i.CreatedAtUtc)
            .ThenBy(i => i.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(TodoItemMappings.ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<TodoItemResponse>(page, query.Page, query.PageSize, totalCount);
    }
}
