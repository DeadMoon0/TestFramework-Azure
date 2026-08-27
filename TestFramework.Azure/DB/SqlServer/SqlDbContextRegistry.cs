using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.DB.SqlServer;

/// <summary>
/// Says how a SQL identifier turns into an EF Core <c>DbContext</c>.
/// </summary>
/// <remarks>
/// <para>
/// A caller registers <em>how to construct</em> their context, and is handed options that already point at
/// the database this run is using. It reads back-to-front from the usual EF registration on purpose: the
/// framework owns the coordinate and the caller owns everything else.
/// </para>
/// <para>
/// <strong>Why not <c>AddDbContext</c>.</strong> That takes the connection string when the registration is
/// built, from a service provider, with no run in sight - so the only way to reach it was for a starting
/// container to write its address into a configuration store the registration then read. That write was the
/// last one of its kind in the family, and it made the store a runtime channel rather than a record of what
/// somebody declared. Handing over the options instead removes the channel rather than hiding it: a context
/// pointing at the wrong database is no longer something a caller can express.
/// </para>
/// <para>
/// The alternative considered and rejected was to let the framework redirect a context after resolving it,
/// with <c>Database.SetConnectionString</c>. It looks smaller and is worse: resolving the context straight
/// from the container would still give you the placeholder, so the rule "always go through the resolver"
/// would be a convention nothing enforces - and because <c>AddDbContext</c> registers scoped, mutating the
/// instance would change it underneath whoever resolved it first.
/// </para>
/// </remarks>
public class SqlDbContextRegistry
{
    private readonly Dictionary<string, Func<RunContext, SqlDatabaseIdentifier, DbContext>> _identifierMap = [];
    private Func<RunContext, SqlDatabaseIdentifier, DbContext>? _default;

    /// <summary>
    /// Registers the context used when no identifier-specific registration matches.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="create">Constructs the context from options pointing at the run's database.</param>
    /// <param name="configure">
    /// Anything else the options should carry - logging, proxies, interceptors. The connection is already set
    /// and overwriting it is the one thing this cannot usefully do.
    /// </param>
    public void AddDefault<TContext>(
        Func<DbContextOptions<TContext>, TContext> create,
        Action<IServiceProvider, DbContextOptionsBuilder<TContext>>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(create);

        _default = (run, identifier) => Build(run, identifier, create, configure);
    }

    /// <summary>
    /// Registers the context for one SQL identifier.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="identifier">The SQL identifier this context serves.</param>
    /// <param name="create">Constructs the context from options pointing at the run's database.</param>
    /// <param name="configure">
    /// Anything else the options should carry - logging, proxies, interceptors. The connection is already set
    /// and overwriting it is the one thing this cannot usefully do.
    /// </param>
    public void AddForIdentifier<TContext>(
        SqlDatabaseIdentifier identifier,
        Func<DbContextOptions<TContext>, TContext> create,
        Action<IServiceProvider, DbContextOptionsBuilder<TContext>>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(create);

        _identifierMap[identifier.Identifier] = (run, resolved) => Build(run, resolved, create, configure);
    }

    internal bool TryCreateByIdentifier(RunContext run, SqlDatabaseIdentifier identifier, out DbContext? context)
    {
        if (_identifierMap.TryGetValue(identifier.Identifier, out Func<RunContext, SqlDatabaseIdentifier, DbContext>? create))
        {
            context = create(run, identifier);
            return true;
        }

        context = null;
        return false;
    }

    internal bool TryCreateDefault(RunContext run, SqlDatabaseIdentifier identifier, out DbContext? context)
    {
        if (_default is not null)
        {
            context = _default(run, identifier);
            return true;
        }

        context = null;
        return false;
    }

    /// <summary>
    /// Builds one context against the database this run is actually using.
    /// </summary>
    /// <remarks>
    /// The connection comes from <see cref="SqlDatabaseResources"/>, which answers whether a person wrote the
    /// address down or a container produced it while starting - so the same registration serves a deployed
    /// database and a containerised one, and neither the caller nor this class learns which happened.
    /// </remarks>
    private static TContext Build<TContext>(
        RunContext run,
        SqlDatabaseIdentifier identifier,
        Func<DbContextOptions<TContext>, TContext> create,
        Action<IServiceProvider, DbContextOptionsBuilder<TContext>>? configure)
        where TContext : DbContext
    {
        SqlDatabaseConfig resolved = SqlDatabaseResources.Resolve(run, identifier);

        DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>();
        builder.UseSqlServer(resolved.ConnectionString);

        configure?.Invoke(run.Services, builder);

        return create(builder.Options);
    }

    internal IReadOnlyCollection<string> RegisteredIdentifiers => _identifierMap.Keys;

    /// <summary>
    /// When set, the framework will apply any pending EF Core migrations (or call EnsureCreated
    /// for contexts with no migrations) the first time each identifier is accessed.
    /// On every access, it will also validate that no migrations are pending.
    /// </summary>
    public void ApplyMigrationsOnFirstUse()
    {
        AutoMigrateOnFirstUse = true;
    }

    internal bool AutoMigrateOnFirstUse { get; private set; }
}
