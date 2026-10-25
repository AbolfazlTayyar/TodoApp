using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Todos.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Application.TodoLists;

internal sealed record GetTodoListsQuery(Guid OwnerId) : IQuery<IReadOnlyList<TodoListResponse>>;

internal sealed record TodoListResponse(
    Guid Id,
    string Name,
    bool IsDefault,
    int TotalItems,
    int CompletedItems,
    DateTime CreatedAtUtc);

internal sealed class GetTodoListsQueryHandler(TodosDbContext dbContext)
    : IQueryHandler<GetTodoListsQuery, IReadOnlyList<TodoListResponse>>
{
    public async Task<Result<IReadOnlyList<TodoListResponse>>> HandleAsync(
        GetTodoListsQuery query,
        CancellationToken cancellationToken)
    {
        // One SQL statement with correlated sub-queries for the counts, instead of N+1 round trips.
        var lists = await dbContext.TodoLists
            .Where(l => l.OwnerId == query.OwnerId)
            .OrderByDescending(l => l.IsDefault)
            .ThenBy(l => l.Name)
            .Select(l => new TodoListResponse(
                l.Id,
                l.Name,
                l.IsDefault,
                dbContext.TodoItems.Count(i => i.ListId == l.Id),
                dbContext.TodoItems.Count(i => i.ListId == l.Id && i.IsCompleted),
                l.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return lists;
    }
}
