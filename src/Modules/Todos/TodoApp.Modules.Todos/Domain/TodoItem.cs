using TodoApp.Shared.Abstractions.Domain;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Domain;

internal enum TodoPriority
{
    Low,
    Medium,
    High
}

/// <summary>
/// Its own aggregate, referencing its list by ID. Small aggregates mean adding one item
/// never loads (or locks) every other item in the list, which matters once lists get large.
/// </summary>
internal sealed class TodoItem : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private TodoItem(
        Guid id,
        Guid listId,
        Guid ownerId,
        string title,
        string? description,
        TodoPriority priority,
        DateTime? dueDateUtc,
        DateTime createdAtUtc)
        : base(id)
    {
        ListId = listId;
        OwnerId = ownerId;
        Title = title;
        Description = description;
        Priority = priority;
        DueDateUtc = dueDateUtc;
        CreatedAtUtc = createdAtUtc;
    }

    private TodoItem()
    {
    }

    public Guid ListId { get; private set; }

    // Denormalized from the list, so "my items" queries filter on one indexed column without a join.
    public Guid OwnerId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public TodoPriority Priority { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Result<TodoItem> Create(
        Guid listId,
        Guid ownerId,
        string title,
        string? description,
        TodoPriority priority,
        DateTime? dueDateUtc,
        DateTime utcNow)
    {
        title = title.Trim();
        description = NormalizeDescription(description);

        if (Validate(title, description) is { } error)
        {
            return error;
        }

        return new TodoItem(Guid.CreateVersion7(), listId, ownerId, title, description, priority, dueDateUtc, utcNow);
    }

    public Result Update(string title, string? description, TodoPriority priority, DateTime? dueDateUtc)
    {
        title = title.Trim();
        description = NormalizeDescription(description);

        if (Validate(title, description) is { } error)
        {
            return error;
        }

        Title = title;
        Description = description;
        Priority = priority;
        DueDateUtc = dueDateUtc;

        return Result.Success();
    }

    public Result Complete(DateTime utcNow)
    {
        if (IsCompleted)
        {
            return TodoErrors.ItemAlreadyCompleted;
        }

        IsCompleted = true;
        CompletedAtUtc = utcNow;

        return Result.Success();
    }

    public Result Reopen()
    {
        if (!IsCompleted)
        {
            return TodoErrors.ItemNotCompleted;
        }

        IsCompleted = false;
        CompletedAtUtc = null;

        return Result.Success();
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static Error? Validate(string title, string? description)
    {
        if (title.Length is 0 or > TitleMaxLength)
        {
            return TodoErrors.InvalidTitle;
        }

        if (description?.Length > DescriptionMaxLength)
        {
            return TodoErrors.DescriptionTooLong;
        }

        return null;
    }
}
