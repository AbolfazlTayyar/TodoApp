using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Domain;

internal static class TodoErrors
{
    // Another user's list or item returns "not found", not "forbidden", so IDs can't be probed.
    public static readonly Error ListNotFound =
        Error.NotFound("Todos.ListNotFound", "The todo list was not found.");

    public static readonly Error ItemNotFound =
        Error.NotFound("Todos.ItemNotFound", "The todo item was not found.");

    public static readonly Error InvalidListName =
        Error.Validation("Todos.InvalidListName", $"List name must be 1 to {TodoList.NameMaxLength} characters.");

    public static readonly Error InvalidTitle =
        Error.Validation("Todos.InvalidTitle", $"Title must be 1 to {TodoItem.TitleMaxLength} characters.");

    public static readonly Error DescriptionTooLong =
        Error.Validation("Todos.DescriptionTooLong", $"Description can't exceed {TodoItem.DescriptionMaxLength} characters.");

    public static readonly Error ItemAlreadyCompleted =
        Error.Conflict("Todos.ItemAlreadyCompleted", "The todo item is already completed.");

    public static readonly Error ItemNotCompleted =
        Error.Conflict("Todos.ItemNotCompleted", "The todo item is not completed.");

    public static readonly Error DefaultListCannotBeDeleted =
        Error.Conflict("Todos.DefaultListCannotBeDeleted", "The default list can't be deleted.");
}
