using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Todos.Domain;
using TodoApp.Modules.Todos.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Application.TodoItems;

internal sealed record UpdateTodoItemCommand(
    Guid OwnerId,
    Guid ItemId,
    string Title,
    string? Description,
    TodoPriority Priority,
    DateTime? DueDateUtc) : ICommand;

internal sealed class UpdateTodoItemCommandHandler(TodosDbContext dbContext) : ICommandHandler<UpdateTodoItemCommand>
{
    public async Task<Result> HandleAsync(UpdateTodoItemCommand command, CancellationToken cancellationToken)
    {
        var item = await dbContext.TodoItems.SingleOrDefaultAsync(
            i => i.Id == command.ItemId && i.OwnerId == command.OwnerId,
            cancellationToken);

        if (item is null)
        {
            return TodoErrors.ItemNotFound;
        }

        var result = item.Update(command.Title, command.Description, command.Priority, command.DueDateUtc);
        if (result.IsFailure)
        {
            return result;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
