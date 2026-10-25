using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TodoApp.Modules.Todos.Infrastructure.Database;
using TodoApp.Shared.Infrastructure.Database;
using TodoApp.Shared.Infrastructure.Messaging;
using TodoApp.Shared.Infrastructure.Modules;

namespace TodoApp.Modules.Todos;

public sealed class TodosModule : IModule
{
    public string Name => "Todos";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TodosDbContext>(configuration, TodosDbContext.Schema);
        services.AddHandlersFromAssembly(typeof(TodosModule).Assembly);
    }
}
