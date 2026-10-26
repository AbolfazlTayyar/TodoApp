using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Modules.Todos.Domain;
using TodoApp.Shared.Infrastructure.Database;

namespace TodoApp.Modules.Todos.Infrastructure.Database;

internal sealed class TodosDbContext(DbContextOptions<TodosDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "todos";

    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TodosDbContext).Assembly);
    }
}

internal sealed class TodoListConfiguration : IEntityTypeConfiguration<TodoList>
{
    public void Configure(EntityTypeBuilder<TodoList> builder)
    {
        builder.ToTable("TodoLists");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Name).HasMaxLength(TodoList.NameMaxLength);

        builder.HasIndex(l => l.OwnerId);

        // The database enforces "one default list per user", even if the same event is handled twice concurrently.
        builder.HasIndex(l => l.OwnerId, "IX_TodoLists_OwnerId_Default")
            .IsUnique()
            .HasFilter("[IsDefault] = 1");
    }
}

internal sealed class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.ToTable("TodoItems");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Title).HasMaxLength(TodoItem.TitleMaxLength);
        builder.Property(i => i.Description).HasMaxLength(TodoItem.DescriptionMaxLength);
        builder.Property(i => i.Priority).HasConversion<string>().HasMaxLength(16);

        // Optimistic concurrency with SQL Server's rowversion: the database changes it on every update,
        // and EF adds "WHERE RowVersion = @original" to each UPDATE. If two requests edit the same item
        // at once, the second one fails. It's a shadow property, so the domain model stays persistence-free.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        // A foreign key is fine *inside* a module. Across modules (OwnerId -> users.Users) there is none,
        // even though both tables now live in the same database.
        builder.HasOne<TodoList>()
            .WithMany()
            .HasForeignKey(i => i.ListId)
            .OnDelete(DeleteBehavior.Cascade);

        // Matches the most common query: "my items, optionally in one list, filtered by status".
        builder.HasIndex(i => new { i.OwnerId, i.ListId, i.IsCompleted });
    }
}
