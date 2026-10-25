using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TodoApp.Shared.Infrastructure.Modules;

/// <summary>
/// The single public entry point of a module. The host only knows this type;
/// everything else inside a module is <c>internal</c>.
/// </summary>
public interface IModule
{
    string Name { get; }

    void Register(IServiceCollection services, IConfiguration configuration);
}
