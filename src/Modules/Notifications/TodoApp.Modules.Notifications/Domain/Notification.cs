using TodoApp.Shared.Abstractions.Domain;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Notifications.Domain;

internal sealed class Notification : Entity
{
    public const int TitleMaxLength = 200;
    public const int MessageMaxLength = 1000;

    private Notification(Guid id, Guid userId, string title, string message, Guid sourceEventId, DateTime createdAtUtc)
        : base(id)
    {
        UserId = userId;
        Title = title;
        Message = message;
        SourceEventId = sourceEventId;
        CreatedAtUtc = createdAtUtc;
    }

    private Notification()
    {
    }

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;

    // The integration event that caused this notification. A unique index on it makes handlers idempotent.
    public Guid SourceEventId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    public static Notification Create(Guid userId, string title, string message, Guid sourceEventId, DateTime utcNow) =>
        new(Guid.CreateVersion7(), userId, Truncate(title, TitleMaxLength), Truncate(message, MessageMaxLength), sourceEventId, utcNow);

    // Idempotent on purpose: marking an already-read notification as read is not an error.
    public void MarkAsRead(DateTime utcNow) => ReadAtUtc ??= utcNow;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

internal static class NotificationErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Notifications.NotFound", "The notification was not found.");
}
