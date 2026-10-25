using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Todos.Domain;
using TodoApp.Modules.Todos.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Application.TodoItems;

internal sealed record GetTodoItemQuery(Guid OwnerId, Guid ItemId) : IQuery<TodoItemResponse>;

internal sealed class GetTodoItemQueryHandler(TodosDbContext dbContext) : IQueryHandler<GetTodoItemQuery, TodoItemResponse>
{
    public async Task<Result<TodoItemResponse>> HandleAsync(GetTodoItemQuery query, CancellationToken cancellationToken)
    {
        var item = await dbContext.TodoItems
            .Where(i => i.Id == query.ItemId && i.OwnerId == query.OwnerId)
            .Select(TodoItemMappings.ToResponse)
            .SingleOrDefaultAsync(cancellationToken);

        return item is null ? TodoErrors.ItemNotFound : item;
    }
}
