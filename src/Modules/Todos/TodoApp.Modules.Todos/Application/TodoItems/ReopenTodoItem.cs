using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Todos.Domain;
using TodoApp.Modules.Todos.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Application.TodoItems;

internal sealed record ReopenTodoItemCommand(Guid OwnerId, Guid ItemId) : ICommand;

internal sealed class ReopenTodoItemCommandHandler(TodosDbContext dbContext) : ICommandHandler<ReopenTodoItemCommand>
{
    public async Task<Result> HandleAsync(ReopenTodoItemCommand command, CancellationToken cancellationToken)
    {
        var item = await dbContext.TodoItems.SingleOrDefaultAsync(
            i => i.Id == command.ItemId && i.OwnerId == command.OwnerId,
            cancellationToken);

        if (item is null)
        {
            return TodoErrors.ItemNotFound;
        }

        var result = item.Reopen();
        if (result.IsFailure)
        {
            return result;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
