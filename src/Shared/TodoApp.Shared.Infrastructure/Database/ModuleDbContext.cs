using Microsoft.EntityFrameworkCore;
using TodoApp.Shared.Abstractions.Events;
using TodoApp.Shared.Infrastructure.Outbox;

namespace TodoApp.Shared.Infrastructure.Database;

/// <summary>
/// Base class for every module's DbContext. It gives each module its own outbox table (in the module's schema)
/// and the UTC date conventions. Derived contexts must call <c>base.OnModelCreating</c>.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Queues an integration event in this module's outbox. It is saved by the next <c>SaveChangesAsync</c>,
    /// in the same transaction as the other changes, and delivered to other modules afterwards.
    /// </summary>
    public void AddIntegrationEvent<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent =>
        OutboxMessages.Add(OutboxMessage.Create(integrationEvent));

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseUtcDateTimes();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
}
