using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Todos.Contracts;
using TodoApp.Modules.Todos.Domain;
using TodoApp.Modules.Todos.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Application.TodoItems;

internal sealed record CompleteTodoItemCommand(Guid OwnerId, Guid ItemId) : ICommand;

internal sealed class CompleteTodoItemCommandHandler(
    TodosDbContext dbContext,
    TimeProvider timeProvider) : ICommandHandler<CompleteTodoItemCommand>
{
    public async Task<Result> HandleAsync(CompleteTodoItemCommand command, CancellationToken cancellationToken)
    {
        var item = await dbContext.TodoItems.SingleOrDefaultAsync(
            i => i.Id == command.ItemId && i.OwnerId == command.OwnerId,
            cancellationToken);

        if (item is null)
        {
            return TodoErrors.ItemNotFound;
        }

        var result = item.Complete(timeProvider.GetUtcNow().UtcDateTime);
        if (result.IsFailure)
        {
            return result;
        }

        // Saved with the change in one transaction, and delivered only after it commits,
        // so consumers never see a fact that didn't happen and never miss one that did.
        dbContext.AddIntegrationEvent(
            new TodoItemCompletedIntegrationEvent(item.Id, item.OwnerId, item.Title, item.CompletedAtUtc!.Value));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
