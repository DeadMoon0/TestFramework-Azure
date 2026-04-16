using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.DB.SqlServer;

public class SqlRowArtifactDescriber<TRow> : ArtifactDescriber<SqlRowArtifactDescriber<TRow>, SqlRowArtifactData<TRow>, SqlRowArtifactReference<TRow>>
    where TRow : class
{
    public override async Task Setup(IServiceProvider serviceProvider, SqlRowArtifactData<TRow> data, SqlRowArtifactReference<TRow> reference, VariableStore variableStore, ScopedLogger logger)
    {
        ISqlDbContextResolver resolver = serviceProvider.GetRequiredService<ISqlDbContextResolver>();
        DbContext context = resolver.Resolve(reference.DbIdentifier);
        await resolver.EnsureReadyAsync(context, reference.DbIdentifier, logger);

        object[] keyValues = reference.GetTypedKeyValues(context, variableStore);

        TRow? existing = await context.Set<TRow>().FindAsync(keyValues);
        if (existing is not null)
        {
            // Detach before attaching the new version to avoid tracking conflict
            context.Entry(existing).State = EntityState.Detached;
            context.Set<TRow>().Update(data.Row);
            logger.LogInformation($"Updating existing SQL row '{typeof(TRow).Name}' ({FormatKeys(keyValues)}).");
        }
        else
        {
            context.Set<TRow>().Add(data.Row);
            logger.LogInformation($"Inserting new SQL row '{typeof(TRow).Name}' ({FormatKeys(keyValues)}).");
        }

        await context.SaveChangesAsync();

        // Detach after save so the entity is not tracked between test stages
        context.Entry(data.Row).State = EntityState.Detached;

        logger.LogInformation($"SQL row '{typeof(TRow).Name}' upserted. DB: {reference.DbIdentifier}, Key: ({FormatKeys(keyValues)}).");
    }

    public override async Task Deconstruct(IServiceProvider serviceProvider, SqlRowArtifactReference<TRow> reference, VariableStore variableStore, ScopedLogger logger)
    {
        ISqlDbContextResolver resolver = serviceProvider.GetRequiredService<ISqlDbContextResolver>();
        DbContext context = resolver.Resolve(reference.DbIdentifier);
        await resolver.EnsureReadyAsync(context, reference.DbIdentifier, logger);

        object[] keyValues = reference.GetTypedKeyValues(context, variableStore);

        TRow? row = await context.Set<TRow>().FindAsync(keyValues);
        if (row is null)
        {
            logger.LogInformation($"SQL row '{typeof(TRow).Name}' ({FormatKeys(keyValues)}) not found during cleanup — already deleted.");
            return;
        }

        context.Set<TRow>().Remove(row);
        await context.SaveChangesAsync();

        logger.LogInformation($"SQL row '{typeof(TRow).Name}' deleted. DB: {reference.DbIdentifier}, Key: ({FormatKeys(keyValues)}).");
    }

    public override string ToString() => $"SQL Row<{typeof(TRow).Name}>";

    private static string FormatKeys(object[] keyValues) => string.Join(", ", keyValues);
}
