using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TodoApp.Modules.Users.Application.Abstractions;
using TodoApp.Modules.Users.Contracts;
using TodoApp.Modules.Users.Infrastructure;
using TodoApp.Modules.Users.Infrastructure.Database;
using TodoApp.Modules.Users.Infrastructure.Security;
using TodoApp.Shared.Infrastructure.Database;
using TodoApp.Shared.Infrastructure.Messaging;
using TodoApp.Shared.Infrastructure.Modules;

namespace TodoApp.Modules.Users;

public sealed class UsersModule : IModule
{
    public string Name => "Users";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<UsersDbContext>(configuration, UsersDbContext.Schema);
        services.AddHandlersFromAssembly(typeof(UsersModule).Assembly);

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenProvider, TokenProvider>();

        services.AddScoped<IUsersModuleApi, UsersModuleApi>();
    }
}
