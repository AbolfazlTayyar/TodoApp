using Microsoft.Extensions.Logging;
using TodoApp.Modules.Notifications.Application.Abstractions;

namespace TodoApp.Modules.Notifications.Infrastructure;

/// <summary>
/// Stand-in for a real provider (SMTP, SendGrid, SES...). Swapping it changes one DI line.
/// </summary>
internal sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        LogEmail(logger, to, subject, body);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sending email to {To}: [{Subject}] {Body}")]
    private static partial void LogEmail(ILogger logger, string to, string subject, string body);
}
