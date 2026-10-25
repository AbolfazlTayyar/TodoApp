using TodoApp.Shared.Abstractions.Domain;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Todos.Domain;

internal sealed class TodoList : Entity
{
    public const int NameMaxLength = 100;
    public const string DefaultListName = "Inbox";

    private TodoList(Guid id, Guid ownerId, string name, bool isDefault, DateTime createdAtUtc)
        : base(id)
    {
        OwnerId = ownerId;
        Name = name;
        IsDefault = isDefault;
        CreatedAtUtc = createdAtUtc;
    }

    private TodoList()
    {
    }

    // A plain ID, not a navigation property: users live in another module (and another database).
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = null!;
    public bool IsDefault { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Result<TodoList> Create(Guid ownerId, string name, DateTime utcNow)
    {
        name = name.Trim();
        if (name.Length is 0 or > NameMaxLength)
        {
            return TodoErrors.InvalidListName;
        }

        return new TodoList(Guid.CreateVersion7(), ownerId, name, isDefault: false, utcNow);
    }

    public static TodoList CreateDefault(Guid ownerId, DateTime utcNow) =>
        new(Guid.CreateVersion7(), ownerId, DefaultListName, isDefault: true, utcNow);

    public Result EnsureCanBeDeleted() =>
        IsDefault ? TodoErrors.DefaultListCannotBeDeleted : Result.Success();
}
