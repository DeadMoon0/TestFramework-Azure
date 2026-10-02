using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Linq;
using System;

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
    ///     reg.AddDefault&lt;MainDbContext&gt;(options =&gt; new MainDbContext(options));
    ///     reg.AddForIdentifier&lt;AuditDbContext&gt;("AuditDb", options =&gt; new AuditDbContext(options));
    /// });
    /// </example>
    /// <remarks>
    /// <para>
    /// There is no <c>AddDbContext</c> beside this. The options handed to each callback already point at the
    /// database this run is using, so a context aimed at the wrong one is not something a caller can write.
    /// </para>
    /// <para>
    /// Call it as often as there are projects declaring contexts: the calls add up, and two that register the same
    /// identifier, or a default each, are refused by name.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Configures the SQL context registry.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddSqlArtifactContexts(
        this IServiceCollection services,
        Action<SqlDbContextRegistry> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        SqlDbContextRegistry declaration = new SqlDbContextRegistry();
        configure(declaration);
        services.AddSingleton(new SqlDbContextDeclaration(declaration));

        // Registered once, built from every declaration: a second call adds to the first instead of replacing it.
        services.TryAddSingleton(sp => SqlDbContextRegistry.Combine(sp.GetServices<SqlDbContextDeclaration>().Select(held => held.Registry)));
        services.TryAddSingleton(sp => new SqlMigrationTracker(sp.GetRequiredService<SqlDbContextRegistry>().AutoMigrateOnFirstUse));

        // No configuration store and no service provider: the registry builds a context from options that
        // point at the run's database, and the run arrives as an argument rather than being fished out of a
        // container. That is what lets a starting database stop writing into somebody else's config store.
        services.TryAddScoped<ISqlDbContextResolver>(sp =>
            new SqlDbContextResolver(
                sp.GetRequiredService<SqlDbContextRegistry>(),
                sp.GetRequiredService<SqlMigrationTracker>()));
        return services;
    }
}

/// <summary>
/// One <c>AddSqlArtifactContexts</c> call's contexts, held until the run combines them.
/// </summary>
/// <param name="Registry">What that call configured.</param>
internal sealed record SqlDbContextDeclaration(SqlDbContextRegistry Registry);
