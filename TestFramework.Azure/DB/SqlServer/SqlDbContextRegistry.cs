using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using TestFramework.Azure.Identifier;

namespace TestFramework.Azure.DB.SqlServer;

public class SqlDbContextRegistry
{
    private readonly Dictionary<string, Func<IServiceProvider, DbContext>> _identifierMap = [];
    private Func<IServiceProvider, DbContext>? _default;

    public void AddDefault<TContext>() where TContext : DbContext
    {
        _default = sp => sp.GetRequiredService<TContext>();
    }

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
