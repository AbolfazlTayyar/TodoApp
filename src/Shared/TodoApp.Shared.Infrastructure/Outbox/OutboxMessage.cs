using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Shared.Abstractions.Events;

namespace TodoApp.Shared.Infrastructure.Outbox;

/// <summary>
/// An integration event waiting to be delivered. Each module has its own <c>OutboxMessages</c> table in its own
/// schema, written by the same <see cref="DbContext"/> and in the same transaction as the state change,
/// so an event exists if and only if the change that caused it was committed.
/// </summary>
public sealed class OutboxMessage
{
    public const int TypeMaxLength = 256;
    public const int ErrorMaxLength = 2000;

    public Guid Id { get; init; }

    /// <summary>The event's full CLR name. Renaming or moving an event class orphans pending messages.</summary>
    public required string Type { get; init; }

    public required string Payload { get; init; }
    public DateTime OccurredOnUtc { get; init; }

    public DateTime? ProcessedOnUtc { get; set; }

    /// <summary>Failed delivery attempts. After the maximum the processor stops retrying and leaves the row for inspection.</summary>
    public int Attempts { get; set; }

    public DateTime NextAttemptOnUtc { get; set; }
    public string? Error { get; set; }

    internal static OutboxMessage Create(IIntegrationEvent integrationEvent) => new()
    {
        Id = integrationEvent.Id,
        Type = integrationEvent.GetType().FullName!,
        // Serialize by runtime type, otherwise only the IIntegrationEvent members would be written.
        Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
        OccurredOnUtc = integrationEvent.OccurredOnUtc,
        NextAttemptOnUtc = integrationEvent.OccurredOnUtc
    };
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(OutboxMessage.TypeMaxLength);
        builder.Property(m => m.Error).HasMaxLength(OutboxMessage.ErrorMaxLength);

        // The processor only ever looks at unprocessed rows, so only those are indexed.
        builder.HasIndex(m => m.NextAttemptOnUtc).HasFilter("[ProcessedOnUtc] IS NULL");

        // Lets the periodic cleanup find old processed rows without scanning the table.
        builder.HasIndex(m => m.ProcessedOnUtc).HasFilter("[ProcessedOnUtc] IS NOT NULL");
    }
}
