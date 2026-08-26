using TestFramework.Core.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.DB.SqlServer;

/// <summary>
/// Finds SQL row artifacts by executing an EF Core query.
/// </summary>
/// <typeparam name="TRow">The row entity type returned by the query.</typeparam>
/// <param name="dbIdentifier">The SQL database identifier.</param>
/// <param name="queryModifier">A delegate that shapes the query before execution.</param>
public class SqlEFCoreArtifactQueryFinder<TRow>(
    SqlDatabaseIdentifier dbIdentifier,
    Func<IQueryable<TRow>, IQueryable<TRow>> queryModifier)
    : ArtifactFinder<SqlRowArtifactDescriber<TRow>, SqlRowArtifactData<TRow>, SqlRowArtifactReference<TRow>>
    where TRow : class
{
    /// <summary>
    /// Finds the first matching SQL row.
    /// </summary>
    public override async Task<ArtifactFinderResult?> FindAsync(RunContext context)
    {
        ISqlDbContextResolver resolver = context.Services.GetRequiredService<ISqlDbContextResolver>();
        DbContext dbContext = resolver.Resolve(dbIdentifier);
        await resolver.EnsureReadyAsync(dbContext, dbIdentifier, context.Logger);

        TRow? entity = await queryModifier(dbContext.Set<TRow>()).FirstOrDefaultAsync(context.Deadline.Token);
        if (entity is null) return null;

        SqlRowArtifactReference<TRow> reference = BuildReference(dbContext, entity);
        return new ArtifactFinderResult(reference);
    }

    /// <summary>
    /// Finds all matching SQL rows.
    /// </summary>
    public override async Task<ArtifactFinderResultMulti> FindMultiAsync(RunContext context)
    {
        ISqlDbContextResolver resolver = context.Services.GetRequiredService<ISqlDbContextResolver>();
        DbContext dbContext = resolver.Resolve(dbIdentifier);
        await resolver.EnsureReadyAsync(dbContext, dbIdentifier, context.Logger);

        List<TRow> entities = await queryModifier(dbContext.Set<TRow>()).ToListAsync(context.Deadline.Token);

        List<ArtifactFinderResult> results = entities
            .Select(entity => new ArtifactFinderResult(BuildReference(dbContext, entity)))
            .ToList();

        return new ArtifactFinderResultMulti(results.ToArray());
    }

    private SqlRowArtifactReference<TRow> BuildReference(DbContext dbContext, TRow entity)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TRow))
            ?? throw new TestFramework.Core.Exceptions.FrameworkConfigurationException($"Entity type '{typeof(TRow).Name}' is not registered in the DbContext.");

        var keyProperties = entityType.FindPrimaryKey()?.Properties
            ?? throw new TestFramework.Core.Exceptions.FrameworkConfigurationException($"No primary key defined for entity '{typeof(TRow).Name}'.");

        // Extract key values as strings from the entity
        string[] pkStringValues = keyProperties
            .Select(prop =>
            {
                object? value = prop.PropertyInfo?.GetValue(entity)
                    ?? throw new TestFramework.Core.Exceptions.FrameworkStateException($"Cannot read key property '{prop.Name}' from entity '{typeof(TRow).Name}'.");
                return Convert.ToString(value) ?? throw new TestFramework.Core.Exceptions.FrameworkStateException($"Key property '{prop.Name}' returned null.");
            })
            .ToArray();

        VariableReference<string>[] pkRefs = pkStringValues.Select(v => Var.Const(v)).ToArray();

        SqlRowArtifactReference<TRow> reference = new SqlRowArtifactReference<TRow>(dbIdentifier, pkRefs);
        // Pre-pin with already-resolved values so reference is immediately usable for cleanup
        reference.PinWithValues(pkStringValues);
        return reference;
    }
}
