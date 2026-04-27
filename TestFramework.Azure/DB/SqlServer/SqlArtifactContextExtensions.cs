using Microsoft.Extensions.DependencyInjection;
using System;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;

namespace TestFramework.Azure.DB.SqlServer;

/// <summary>
/// Registers the SQL services required by SQL-backed Azure artifacts and query finders.
/// </summary>
public static class SqlArtifactContextExtensions
{
    /// <summary>
    /// Registers the SQL artifact context resolver.
    /// Use the registry to map SqlDatabaseIdentifiers or set a default DbContext.
    /// </summary>
    /// <example>
    /// services.AddSqlArtifactContexts(reg =>
    /// {
    ///     reg.AddDefault&lt;MainDbContext&gt;();
    ///     reg.AddForIdentifier&lt;AuditDbContext&gt;("AuditDb");
    /// });
    /// </example>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Configures the SQL context registry.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddSqlArtifactContexts(
        this IServiceCollection services,
        Action<SqlDbContextRegistry> configure)
    {
        SqlDbContextRegistry registry = new SqlDbContextRegistry();
        configure(registry);
        services.AddSingleton(registry);
        services.AddSingleton(new SqlMigrationTracker(registry.AutoMigrateOnFirstUse));
        services.AddScoped<ISqlDbContextResolver>(sp =>
            new SqlDbContextResolver(
                sp.GetRequiredService<SqlDbContextRegistry>(),
                sp.GetRequiredService<ConfigStore<SqlDatabaseConfig>>(),
                sp,
                sp.GetRequiredService<SqlMigrationTracker>()));
        return services;
    }
}
