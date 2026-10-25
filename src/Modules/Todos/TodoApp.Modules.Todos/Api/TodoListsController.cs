using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Modules.Todos.Application.TodoLists;
using TodoApp.Modules.Todos.Domain;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Infrastructure.Api;

namespace TodoApp.Modules.Todos.Api;

internal sealed record CreateTodoListRequest([Required, MaxLength(TodoList.NameMaxLength)] string Name);

internal sealed record CreatedResponse(Guid Id);

[Route("api/todo-lists")]
internal sealed class TodoListsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TodoListResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAll(
        [FromServices] IQueryHandler<GetTodoListsQuery, IReadOnlyList<TodoListResponse>> handler,
        CancellationToken cancellationToken) =>
        OkOrProblem(await handler.HandleAsync(new GetTodoListsQuery(CurrentUserId), cancellationToken));

    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create(
        CreateTodoListRequest request,
        [FromServices] ICommandHandler<CreateTodoListCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CreateTodoListCommand(CurrentUserId, request.Name), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetAll), new CreatedResponse(result.Value))
            : ToProblem(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteTodoListCommand> handler,
        CancellationToken cancellationToken) =>
        NoContentOrProblem(await handler.HandleAsync(new DeleteTodoListCommand(CurrentUserId, id), cancellationToken));
}
