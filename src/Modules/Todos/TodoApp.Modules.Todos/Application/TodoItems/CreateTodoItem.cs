using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Todos.Domain;
using TodoApp.Modules.Todos.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Application.TodoItems;

internal sealed record CreateTodoItemCommand(
    Guid OwnerId,
    Guid ListId,
    string Title,
    string? Description,
    TodoPriority Priority,
    DateTime? DueDateUtc) : ICommand<Guid>;

internal sealed class CreateTodoItemCommandHandler(TodosDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<CreateTodoItemCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateTodoItemCommand command, CancellationToken cancellationToken)
    {
        var listExists = await dbContext.TodoLists.AnyAsync(
            l => l.Id == command.ListId && l.OwnerId == command.OwnerId,
            cancellationToken);

        if (!listExists)
        {
            return TodoErrors.ListNotFound;
        }

        var result = TodoItem.Create(
            command.ListId,
            command.OwnerId,
            command.Title,
            command.Description,
            command.Priority,
            command.DueDateUtc,
            timeProvider.GetUtcNow().UtcDateTime);

        if (result.IsFailure)
        {
            return result.Error;
        }

        dbContext.TodoItems.Add(result.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return result.Value.Id;
    }
}
