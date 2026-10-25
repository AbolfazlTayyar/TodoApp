namespace TodoApp.Shared.Abstractions.Domain;

public abstract class Entity
{
    protected Entity(Guid id) => Id = id;

    // Required by EF Core when it materializes entities from the database.
    protected Entity()
    {
    }

    public Guid Id { get; private init; }
}
