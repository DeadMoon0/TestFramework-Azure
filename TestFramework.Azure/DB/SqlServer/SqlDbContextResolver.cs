using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Logging;

namespace TestFramework.Azure.DB.SqlServer;

internal class SqlDbContextResolver(SqlDbContextRegistry registry, ConfigStore<SqlDatabaseConfig> configStore, IServiceProvider serviceProvider, SqlMigrationTracker migrationTracker) : ISqlDbContextResolver
{
    public DbContext Resolve(SqlDatabaseIdentifier identifier)
    {
        // 1. Identifier-based registration takes priority
        if (registry.TryResolveByIdentifier(identifier, serviceProvider, out DbContext? context) && context is not null)
            return context;

        // 2. ContextType in config — resolve CLR type from DI
        SqlDatabaseConfig config = configStore.GetConfig(identifier);
        if (!string.IsNullOrWhiteSpace(config.ContextType))
        {
            Type? contextType = Type.GetType(config.ContextType, throwOnError: false);
            if (contextType is null)
            {
                // Try scanning loaded assemblies by full name
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    contextType = asm.GetType(config.ContextType, throwOnError: false);
                    if (contextType is not null) break;
                }
            }

            if (contextType is null)
                throw new InvalidOperationException(
                    $"Could not resolve CLR type '{config.ContextType}' configured for SqlDatabase identifier '{identifier}'. " +
                    $"Ensure the assembly is loaded and the type name is correct.");

            if (!typeof(DbContext).IsAssignableFrom(contextType))
                throw new InvalidOperationException(
                    $"The configured ContextType '{config.ContextType}' for identifier '{identifier}' does not inherit from DbContext.");

            DbContext contextFromType = (DbContext)serviceProvider.GetRequiredService(contextType);
            return contextFromType;
        }

        // 3. Default registration fallback
        if (registry.TryResolveDefault(serviceProvider, out DbContext? defaultContext) && defaultContext is not null)
            return defaultContext;

        throw new InvalidOperationException(
            $"No DbContext could be resolved for SqlDatabase identifier '{identifier}'. " +
            $"Register a context via AddSqlArtifactContexts(reg => reg.AddForIdentifier<TContext>(\"{identifier}\")) " +
            $"or reg.AddDefault<TContext>(), or set ContextType in config.");
    }

    public Task EnsureReadyAsync(DbContext context, SqlDatabaseIdentifier identifier, ScopedLogger logger)
        => migrationTracker.EnsureReadyAsync(context, identifier, logger);
}
