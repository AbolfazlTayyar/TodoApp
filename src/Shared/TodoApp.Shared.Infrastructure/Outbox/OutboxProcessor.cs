using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TodoApp.Shared.Infrastructure.Outbox;

/// <summary>
/// One background service for every module's outbox. Modules own their tables; this just visits each of them,
/// so adding a module adds no extra threads or hosted services.
/// </summary>
/// <remarks>
/// Register it after all modules (see <c>AddOutboxProcessor</c>): hosted services start in registration order,
/// and the modules' migrators must have created the outbox tables before the first poll.
/// </remarks>
internal sealed class OutboxProcessor(
    IEnumerable<IOutboxStore> stores,
    OutboxSignal signal,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan ProcessedRetention = TimeSpan.FromDays(7);

    private readonly IOutboxStore[] _stores = stores.ToArray();
    private DateTime _lastCleanupUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var taken = 0;

            foreach (var store in _stores)
            {
                try
                {
                    taken += await store.ProcessBatchAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // One module's database trouble must not stop the other modules' events.
                    OutboxLog.StoreFailed(logger, ex, store.Name);
                }
            }

            await CleanUpIfDueAsync(stoppingToken);

            // A full pass that found nothing: sleep until a module saves something, or the poll interval passes.
            // Otherwise go straight back for the next batch.
            if (taken == 0)
            {
                await signal.WaitAsync(PollInterval, stoppingToken);
            }
        }
    }

    private async Task CleanUpIfDueAsync(CancellationToken stoppingToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (now - _lastCleanupUtc < CleanupInterval)
        {
            return;
        }

        _lastCleanupUtc = now;

        foreach (var store in _stores)
        {
            try
            {
                await store.DeleteProcessedBeforeAsync(now - ProcessedRetention, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                OutboxLog.CleanupFailed(logger, ex, store.Name);
            }
        }
    }
}
