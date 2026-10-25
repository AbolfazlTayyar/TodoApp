using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TodoApp.Shared.Infrastructure.Database;

public static class DatabaseExtensions
{
    private const string ConnectionStringName = "Database";

    /// <summary>
    /// All modules share ONE database, but each module owns its own schema (users, todos, notifications)
    /// and its own DbContext. A module never maps, joins or writes another module's tables, so a module
    /// can still be moved to its own database later by changing only its connection string.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema)
        where TContext : DbContext
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
        }

        services.AddDbContext<TContext>(options => options.UseSqlServer(connectionString, sql => sql
            // Each module keeps its migration history in its own schema, so modules migrate independently.
            .MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema)
            // Retries transient errors (failover, Azure SQL throttling, a server still starting up).
            // Note: explicit transactions then have to run inside Database.CreateExecutionStrategy().
            .EnableRetryOnFailure()));

        services.AddHostedService<DatabaseMigrator<TContext>>();

        return services;
    }
}

/// <remarks>
/// Applying migrations on startup is convenient for a sample. With many instances in production,
/// run migrations as a separate deployment step instead (e.g. <c>dotnet ef migrations bundle</c>).
/// </remarks>
internal sealed class DatabaseMigrator<TContext>(IServiceScopeFactory scopeFactory) : IHostedService
    where TContext : DbContext
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

        // Creates the database on first run, then this module's schema and tables.
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
