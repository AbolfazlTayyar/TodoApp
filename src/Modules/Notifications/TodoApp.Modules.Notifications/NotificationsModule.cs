using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TodoApp.Modules.Notifications.Application.Abstractions;
using TodoApp.Modules.Notifications.Infrastructure;
using TodoApp.Modules.Notifications.Infrastructure.Database;
using TodoApp.Shared.Infrastructure.Database;
using TodoApp.Shared.Infrastructure.Messaging;
using TodoApp.Shared.Infrastructure.Modules;

namespace TodoApp.Modules.Notifications;

public sealed class NotificationsModule : IModule
{
    public string Name => "Notifications";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<NotificationsDbContext>(configuration, NotificationsDbContext.Schema);
        services.AddHandlersFromAssembly(typeof(NotificationsModule).Assembly);

        services.AddSingleton<IEmailSender, LoggingEmailSender>();
    }
}
