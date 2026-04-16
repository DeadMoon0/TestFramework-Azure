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

public class SqlEFCoreArtifactQueryFinder<TRow>(
    SqlDatabaseIdentifier dbIdentifier,
    Func<IQueryable<TRow>, IQueryable<TRow>> queryModifier)
    : ArtifactFinder<SqlRowArtifactDescriber<TRow>, SqlRowArtifactData<TRow>, SqlRowArtifactReference<TRow>>
    where TRow : class
{
    public override async Task<ArtifactFinderResult?> FindAsync(
        IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ISqlDbContextResolver resolver = serviceProvider.GetRequiredService<ISqlDbContextResolver>();
        DbContext context = resolver.Resolve(dbIdentifier);
        await resolver.EnsureReadyAsync(context, dbIdentifier, logger);

        TRow? entity = await queryModifier(context.Set<TRow>()).FirstOrDefaultAsync(cancellationToken);
        if (entity is null) return null;

        SqlRowArtifactReference<TRow> reference = BuildReference(context, entity);
        return new ArtifactFinderResult(reference);
    }

    public override async Task<ArtifactFinderResultMulti> FindMultiAsync(
        IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ISqlDbContextResolver resolver = serviceProvider.GetRequiredService<ISqlDbContextResolver>();
        DbContext context = resolver.Resolve(dbIdentifier);
        await resolver.EnsureReadyAsync(context, dbIdentifier, logger);

        List<TRow> entities = await queryModifier(context.Set<TRow>()).ToListAsync(cancellationToken);

        List<ArtifactFinderResult> results = entities
            .Select(entity => new ArtifactFinderResult(BuildReference(context, entity)))
            .ToList();

        return new ArtifactFinderResultMulti(results.ToArray());
    }

    private SqlRowArtifactReference<TRow> BuildReference(DbContext context, TRow entity)
    {
        var entityType = context.Model.FindEntityType(typeof(TRow))
            ?? throw new InvalidOperationException($"Entity type '{typeof(TRow).Name}' is not registered in the DbContext.");

        var keyProperties = entityType.FindPrimaryKey()?.Properties
            ?? throw new InvalidOperationException($"No primary key defined for entity '{typeof(TRow).Name}'.");

        // Extract key values as strings from the entity
        string[] pkStringValues = keyProperties
            .Select(prop =>
            {
                object? value = prop.PropertyInfo?.GetValue(entity)
                    ?? throw new InvalidOperationException($"Cannot read key property '{prop.Name}' from entity '{typeof(TRow).Name}'.");
                return Convert.ToString(value) ?? throw new InvalidOperationException($"Key property '{prop.Name}' returned null.");
            })
            .ToArray();

        VariableReference<string>[] pkRefs = pkStringValues.Select(v => Var.Const(v)).ToArray();

        SqlRowArtifactReference<TRow> reference = new SqlRowArtifactReference<TRow>(dbIdentifier, pkRefs);
        // Pre-pin with already-resolved values so reference is immediately usable for cleanup
        reference.PinWithValues(pkStringValues);
        return reference;
    }
}
