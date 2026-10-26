using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TodoApp.Shared.Abstractions.Events;
using TodoApp.Shared.Infrastructure.Database;
using TodoApp.Shared.Infrastructure.Events;

namespace TodoApp.Shared.Infrastructure.Outbox;

/// <summary>One module's outbox, as seen by the single shared <see cref="OutboxProcessor"/>.</summary>
internal interface IOutboxStore
{
    string Name { get; }

    /// <summary>Delivers one batch of due messages. Returns how many were taken, so the caller knows whether to loop.</summary>
    Task<int> ProcessBatchAsync(CancellationToken cancellationToken);

    Task DeleteProcessedBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
}

internal sealed class OutboxStore<TContext>(
    IServiceScopeFactory scopeFactory,
    IntegrationEventDispatcher dispatcher,
    TimeProvider timeProvider,
    ILogger<OutboxStore<TContext>> logger) : IOutboxStore
    where TContext : ModuleDbContext
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 8;
    private const int MaxBackoffSeconds = 300;

    private string? _claimSql;

    public string Name => typeof(TContext).Name;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

        // The context uses EnableRetryOnFailure, which requires explicit transactions to run inside the strategy.
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            // UPDLOCK + READPAST: with several instances running, each one claims different rows
            // instead of waiting on, or double-processing, the rows another instance holds.
            _claimSql ??= BuildClaimSql(dbContext);
            var messages = await dbContext.OutboxMessages
                .FromSqlRaw(_claimSql, timeProvider.GetUtcNow().UtcDateTime)
                .ToListAsync(cancellationToken);

            foreach (var message in messages)
            {
                var error = await TryDeliverAsync(message, cancellationToken);
                var now = timeProvider.GetUtcNow().UtcDateTime;

                if (error is null)
                {
                    message.ProcessedOnUtc = now;
                    message.Error = null;
                    continue;
                }

                message.Attempts++;
                message.Error = error.Length > OutboxMessage.ErrorMaxLength ? error[..OutboxMessage.ErrorMaxLength] : error;
                message.NextAttemptOnUtc = now.AddSeconds(Math.Min(Math.Pow(2, message.Attempts), MaxBackoffSeconds));

                if (message.Attempts >= MaxAttempts)
                {
                    OutboxLog.GaveUp(logger, Name, message.Type, message.Id, message.Attempts);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return messages.Count;
        });
    }

    public async Task DeleteProcessedBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

        await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc != null && m.ProcessedOnUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <returns>null when the message was delivered; otherwise the reason it must be retried.</returns>
    private async Task<string?> TryDeliverAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (!dispatcher.TryResolveEventType(message.Type, out var eventType))
        {
            // No handler consumes this event type, so there is nobody to deliver it to.
            OutboxLog.NoHandlers(logger, Name, message.Type, message.Id);
            return null;
        }

        IIntegrationEvent? integrationEvent;
        try
        {
            integrationEvent = (IIntegrationEvent?)JsonSerializer.Deserialize(message.Payload, eventType);
        }
        catch (JsonException ex)
        {
            return $"Payload could not be deserialized: {ex.Message}";
        }

        if (integrationEvent is null)
        {
            return "Payload deserialized to null.";
        }

        var failedHandlers = await dispatcher.DispatchAsync(integrationEvent, cancellationToken);
        return failedHandlers.Count == 0 ? null : $"Handlers failed: {string.Join(", ", failedHandlers)}";
    }

    // The table name comes from the EF model (never from user input), so building the SQL text is safe.
    // The batch size and attempt limit are constants; the due time is passed as a real parameter ({0}).
    private static string BuildClaimSql(TContext dbContext)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(OutboxMessage))!;
        var table = $"[{entityType.GetSchema()}].[{entityType.GetTableName()}]";

        // $$ raw string: "{{x}}" interpolates, a single "{0}" stays literal text for FromSqlRaw.
        return $$"""
            SELECT TOP ({{BatchSize}}) *
            FROM {{table}} WITH (UPDLOCK, READPAST, ROWLOCK)
            WHERE [ProcessedOnUtc] IS NULL AND [Attempts] < {{MaxAttempts}} AND [NextAttemptOnUtc] <= {0}
            ORDER BY [NextAttemptOnUtc], [OccurredOnUtc]
            """;
    }
}

// Separate non-generic class: the logging source generator doesn't need to cope with OutboxStore<TContext>.
internal static partial class OutboxLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "{Store}: gave up on {EventType} {EventId} after {Attempts} attempts; inspect OutboxMessages")]
    public static partial void GaveUp(ILogger logger, string store, string eventType, Guid eventId, int attempts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Store}: no handler for {EventType} {EventId}; marked as processed")]
    public static partial void NoHandlers(ILogger logger, string store, string eventType, Guid eventId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox {Store} failed; will retry")]
    public static partial void StoreFailed(ILogger logger, Exception exception, string store);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox cleanup failed for {Store}")]
    public static partial void CleanupFailed(ILogger logger, Exception exception, string store);
}
