using TestFramework.Core.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.DB.SqlServer;

/// <summary>
/// Sets up and tears down SQL row artifacts.
/// </summary>
/// <typeparam name="TRow">The row entity type.</typeparam>
public class AzureSqlRowArtifactDescriber<TRow> : ArtifactDescriber<AzureSqlRowArtifactDescriber<TRow>, AzureSqlRowArtifactData<TRow>, AzureSqlRowArtifactReference<TRow>>
    where TRow : class
{
    /// <summary>
    /// The canonical key mirrored from <c>TestFramework.Core.Debugger.DebugValueSchemaKeys.SqlRow</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately the same key the ADO-backed SQL row artifact in TestFramework.Web reports: both
    /// present a database row, so a consumer should draw them with one icon and one inspector.
    /// A literal rather than a reference because this package builds against the published Core;
    /// <c>ArtifactSchemaKeyTests</c> pins it to the canonical value so the two cannot drift.
    /// </remarks>
    internal const string SchemaKey = "tf.artifact.sql.row";

    /// <summary>
    /// Identifies this artifact's debug shape, so a consumer renders it as a database row instead of
    /// falling back to a generic view keyed on the CLR type name.
    /// </summary>
    public override string DebugValueSchemaKey => SchemaKey;

    /// <inheritdoc />
    public override JToken? CreateDebugValueCustomPayload(ArtifactInstanceGeneric instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (instance.Reference is not AzureSqlRowArtifactReference<TRow>)
            return null;

        return new JObject
        {
            ["key"] = instance.Identifier.Identifier,
            ["itemType"] = typeof(TRow).Name
        };
    }

    /// <summary>
    /// Serializes setup work for SQL row artifacts by shared database resource.
    /// </summary>
    public override ArtifactSetupParallelizationMode SetupParallelization => ArtifactSetupParallelizationMode.SerializeByArtifactType;

    /// <summary>
    /// Groups setup work by SQL database so multiple rows targeting the same DbContext do not execute concurrently.
    /// </summary>
    public override string? GetSetupParallelizationResourceKey(ArtifactInstanceGeneric artifactInstance)
    {
        if (artifactInstance.Reference is not AzureSqlRowArtifactReference<TRow> reference)
            return base.GetSetupParallelizationResourceKey(artifactInstance);

        return $"sql-row:{reference.DbIdentifier}";
    }

    /// <summary>
    /// Ensures the referenced row exists during artifact setup.
    /// </summary>
    public override async Task Setup(RunContext context, AzureSqlRowArtifactData<TRow> data, AzureSqlRowArtifactReference<TRow> reference)
    {
        ISqlDbContextResolver resolver = context.Services.GetRequiredService<ISqlDbContextResolver>();

        // Disposed here because Resolve creates a fresh context per call, and a context nobody
        // disposes holds its connection until the GC gets around to it.
        await using DbContext dbContext = resolver.Resolve(context, reference.DbIdentifier);
        await resolver.EnsureReadyAsync(context, dbContext, reference.DbIdentifier);

        object[] keyValues = reference.GetTypedKeyValues(dbContext, context.Variables);

        TRow? existing = await dbContext.Set<TRow>().FindAsync(keyValues, context.Deadline.Token);
        if (existing is not null)
        {
            // Detach before attaching the new version to avoid tracking conflict
            dbContext.Entry(existing).State = EntityState.Detached;
            dbContext.Set<TRow>().Update(data.Row);
            context.Logger.LogInformation($"Updating existing SQL row '{typeof(TRow).Name}' ({FormatKeys(keyValues)}).");
        }
        else
        {
            dbContext.Set<TRow>().Add(data.Row);
            context.Logger.LogInformation($"Inserting new SQL row '{typeof(TRow).Name}' ({FormatKeys(keyValues)}).");
        }

        await dbContext.SaveChangesAsync(context.Deadline.Token);

        // Detach after save so the entity is not tracked between test stages
        dbContext.Entry(data.Row).State = EntityState.Detached;

        context.Logger.LogInformation($"SQL row '{typeof(TRow).Name}' upserted. DB: {reference.DbIdentifier}, Key: ({FormatKeys(keyValues)}).");
    }

    /// <summary>
    /// Removes the referenced row during artifact cleanup.
    /// </summary>
    public override async Task Deconstruct(RunContext context, AzureSqlRowArtifactReference<TRow> reference)
    {
        ISqlDbContextResolver resolver = context.Services.GetRequiredService<ISqlDbContextResolver>();
        await using DbContext dbContext = resolver.Resolve(context, reference.DbIdentifier);
        await resolver.EnsureReadyAsync(context, dbContext, reference.DbIdentifier);

        object[] keyValues = reference.GetTypedKeyValues(dbContext, context.Variables);

        TRow? row = await dbContext.Set<TRow>().FindAsync(keyValues, context.Deadline.Token);
        if (row is null)
        {
            context.Logger.LogInformation($"SQL row '{typeof(TRow).Name}' ({FormatKeys(keyValues)}) not found during cleanup — already deleted.");
            return;
        }

        dbContext.Set<TRow>().Remove(row);
        await dbContext.SaveChangesAsync(context.Deadline.Token);

        context.Logger.LogInformation($"SQL row '{typeof(TRow).Name}' deleted. DB: {reference.DbIdentifier}, Key: ({FormatKeys(keyValues)}).");
    }

    /// <summary>
    /// Returns a readable string representation of the describer.
    /// </summary>
    /// <returns>A string representation of the describer.</returns>
    public override string ToString() => $"SQL Row<{typeof(TRow).Name}>";

    private static string FormatKeys(object[] keyValues) => string.Join(", ", keyValues);
}
