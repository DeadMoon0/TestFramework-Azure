using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using TestFramework.Azure.Identifier;

namespace TestFramework.Azure.DB.SqlServer;

/// <summary>
/// Registers how SQL identifiers map to EF Core <c>DbContext</c> types.
/// </summary>
public class SqlDbContextRegistry
{
    private readonly Dictionary<string, Func<IServiceProvider, DbContext>> _identifierMap = [];
    private Func<IServiceProvider, DbContext>? _default;

    /// <summary>
    /// Registers the default <c>DbContext</c> used when no identifier-specific registration matches.
    /// </summary>
    /// <typeparam name="TContext">The <c>DbContext</c> type to resolve from DI.</typeparam>
    public void AddDefault<TContext>() where TContext : DbContext
    {
        _default = sp => sp.GetRequiredService<TContext>();
    }

    /// <summary>
    /// Registers a specific <c>DbContext</c> for a SQL identifier.
    /// </summary>
    /// <typeparam name="TContext">The <c>DbContext</c> type to resolve from DI.</typeparam>
    /// <param name="identifier">The SQL identifier that should resolve to the context.</param>
    public void AddForIdentifier<TContext>(SqlDatabaseIdentifier identifier) where TContext : DbContext
    {
        _identifierMap[identifier.Identifier] = sp => sp.GetRequiredService<TContext>();
    }

    internal bool TryResolveByIdentifier(SqlDatabaseIdentifier identifier, IServiceProvider sp, out DbContext? context)
    {
        if (_identifierMap.TryGetValue(identifier.Identifier, out var factory))
        {
            context = factory(sp);
            return true;
        }
        context = null;
        return false;
    }

    internal bool TryResolveDefault(IServiceProvider sp, out DbContext? context)
    {
        if (_default is not null)
        {
            context = _default(sp);
            return true;
        }
        context = null;
        return false;
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
