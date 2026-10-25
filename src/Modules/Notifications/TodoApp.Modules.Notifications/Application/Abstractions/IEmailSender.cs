namespace TodoApp.Modules.Notifications.Application.Abstractions;

internal interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}
