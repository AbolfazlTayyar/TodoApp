using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Todos.Domain;
using TodoApp.Modules.Todos.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Application.TodoLists;

internal sealed record DeleteTodoListCommand(Guid OwnerId, Guid ListId) : ICommand;

internal sealed class DeleteTodoListCommandHandler(TodosDbContext dbContext) : ICommandHandler<DeleteTodoListCommand>
{
    public async Task<Result> HandleAsync(DeleteTodoListCommand command, CancellationToken cancellationToken)
    {
        var list = await dbContext.TodoLists.SingleOrDefaultAsync(
            l => l.Id == command.ListId && l.OwnerId == command.OwnerId,
            cancellationToken);

        if (list is null)
        {
            return TodoErrors.ListNotFound;
        }

        var canDelete = list.EnsureCanBeDeleted();
        if (canDelete.IsFailure)
        {
            return canDelete;
        }

        // The list's items are removed by the database's ON DELETE CASCADE.
        dbContext.TodoLists.Remove(list);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
