using TodoApp.Modules.Todos.Domain;
using TodoApp.Modules.Todos.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Application.TodoLists;

internal sealed record CreateTodoListCommand(Guid OwnerId, string Name) : ICommand<Guid>;

internal sealed class CreateTodoListCommandHandler(TodosDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<CreateTodoListCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateTodoListCommand command, CancellationToken cancellationToken)
    {
        var result = TodoList.Create(command.OwnerId, command.Name, timeProvider.GetUtcNow().UtcDateTime);
        if (result.IsFailure)
        {
            return result.Error;
        }

        dbContext.TodoLists.Add(result.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return result.Value.Id;
    }
}
