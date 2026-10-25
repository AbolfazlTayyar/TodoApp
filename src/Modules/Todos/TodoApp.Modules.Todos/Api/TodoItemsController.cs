using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Modules.Todos.Application.TodoItems;
using TodoApp.Modules.Todos.Domain;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Pagination;
using TodoApp.Shared.Infrastructure.Api;

namespace TodoApp.Modules.Todos.Api;

internal sealed record CreateTodoItemRequest(
    Guid ListId,
    [Required, MaxLength(TodoItem.TitleMaxLength)] string Title,
    [MaxLength(TodoItem.DescriptionMaxLength)] string? Description,
    TodoPriority Priority = TodoPriority.Medium,
    DateTime? DueDateUtc = null);

internal sealed record UpdateTodoItemRequest(
    [Required, MaxLength(TodoItem.TitleMaxLength)] string Title,
    [MaxLength(TodoItem.DescriptionMaxLength)] string? Description,
    TodoPriority Priority,
    DateTime? DueDateUtc);

[Route("api/todo-items")]
internal sealed class TodoItemsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<TodoItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPage(
        [FromServices] IQueryHandler<GetTodoItemsQuery, PagedResult<TodoItemResponse>> handler,
        [FromQuery] Guid? listId,
        [FromQuery] bool? isCompleted,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        OkOrProblem(await handler.HandleAsync(
            new GetTodoItemsQuery(CurrentUserId, listId, isCompleted, page, pageSize),
            cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TodoItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById(
        Guid id,
        [FromServices] IQueryHandler<GetTodoItemQuery, TodoItemResponse> handler,
        CancellationToken cancellationToken) =>
        OkOrProblem(await handler.HandleAsync(new GetTodoItemQuery(CurrentUserId, id), cancellationToken));

    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Create(
        CreateTodoItemRequest request,
        [FromServices] ICommandHandler<CreateTodoItemCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateTodoItemCommand(
            CurrentUserId,
            request.ListId,
            request.Title,
            request.Description,
            request.Priority,
            request.DueDateUtc);

        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new CreatedResponse(result.Value))
            : ToProblem(result.Error);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(
        Guid id,
        UpdateTodoItemRequest request,
        [FromServices] ICommandHandler<UpdateTodoItemCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTodoItemCommand(
            CurrentUserId,
            id,
            request.Title,
            request.Description,
            request.Priority,
            request.DueDateUtc);

        return NoContentOrProblem(await handler.HandleAsync(command, cancellationToken));
    }

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Complete(
        Guid id,
        [FromServices] ICommandHandler<CompleteTodoItemCommand> handler,
        CancellationToken cancellationToken) =>
        NoContentOrProblem(await handler.HandleAsync(new CompleteTodoItemCommand(CurrentUserId, id), cancellationToken));

    [HttpPost("{id:guid}/reopen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Reopen(
        Guid id,
        [FromServices] ICommandHandler<ReopenTodoItemCommand> handler,
        CancellationToken cancellationToken) =>
        NoContentOrProblem(await handler.HandleAsync(new ReopenTodoItemCommand(CurrentUserId, id), cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteTodoItemCommand> handler,
        CancellationToken cancellationToken) =>
        NoContentOrProblem(await handler.HandleAsync(new DeleteTodoItemCommand(CurrentUserId, id), cancellationToken));
}
