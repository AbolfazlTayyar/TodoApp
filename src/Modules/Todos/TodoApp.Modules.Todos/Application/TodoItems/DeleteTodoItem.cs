using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Todos.Domain;
using TodoApp.Modules.Todos.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Application.TodoItems;

internal sealed record DeleteTodoItemCommand(Guid OwnerId, Guid ItemId) : ICommand;

internal sealed class DeleteTodoItemCommandHandler(TodosDbContext dbContext) : ICommandHandler<DeleteTodoItemCommand>
{
    public async Task<Result> HandleAsync(DeleteTodoItemCommand command, CancellationToken cancellationToken)
    {
        // A single DELETE statement: no need to load an entity just to remove it.
        var deleted = await dbContext.TodoItems
            .Where(i => i.Id == command.ItemId && i.OwnerId == command.OwnerId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? TodoErrors.ItemNotFound : Result.Success();
    }
}
