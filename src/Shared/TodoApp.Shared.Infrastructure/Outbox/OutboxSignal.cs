using Microsoft.EntityFrameworkCore.Diagnostics;
using TodoApp.Shared.Infrastructure.Database;

namespace TodoApp.Shared.Infrastructure.Outbox;

/// <summary>
/// Wakes the <see cref="OutboxProcessor"/> right after a module saves, so events are delivered within
/// milliseconds instead of waiting for the next poll. Polling stays as the safety net (other instances,
/// missed wake-ups, retries that have become due).
/// </summary>
internal sealed class OutboxSignal
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Notify()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // A wake-up is already pending; one is enough.
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _semaphore.WaitAsync(timeout, cancellationToken);
}

/// <remarks>
/// Fires after any successful save of a module context, not only saves that added an outbox row. The cost of a
/// spurious wake-up is one empty indexed query, which is cheaper than tracking which saves carried events.
/// </remarks>
internal sealed class OutboxSignalInterceptor(OutboxSignal signal) : SaveChangesInterceptor
{
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        NotifyIfModuleContext(eventData);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        NotifyIfModuleContext(eventData);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void NotifyIfModuleContext(SaveChangesCompletedEventData eventData)
    {
        if (eventData.Context is ModuleDbContext)
        {
            signal.Notify();
        }
    }
}
