using System.Linq.Expressions;
using TodoApp.Modules.Todos.Domain;

namespace TodoApp.Modules.Todos.Application.TodoItems;

internal sealed record TodoItemResponse(
    Guid Id,
    Guid ListId,
    string Title,
    string? Description,
    TodoPriority Priority,
    DateTime? DueDateUtc,
    bool IsCompleted,
    DateTime? CompletedAtUtc,
    DateTime CreatedAtUtc);

internal static class TodoItemMappings
{
    // An expression (not a method), so EF Core translates it to SQL and selects only these columns.
    public static readonly Expression<Func<TodoItem, TodoItemResponse>> ToResponse = item => new TodoItemResponse(
        item.Id,
        item.ListId,
        item.Title,
        item.Description,
        item.Priority,
        item.DueDateUtc,
        item.IsCompleted,
        item.CompletedAtUtc,
        item.CreatedAtUtc);
}
