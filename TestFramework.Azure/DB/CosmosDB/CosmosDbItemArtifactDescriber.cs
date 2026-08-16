using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Runtime;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.DB.CosmosDB;

/// <summary>
/// Sets up and tears down Cosmos DB item artifacts.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
public class CosmosDbItemArtifactDescriber<TItem> : ArtifactDescriber<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactReference<TItem>>
{
    /// <summary>
    /// The canonical key mirrored from <c>TestFramework.Core.Debugger.DebugValueSchemaKeys.CosmosItem</c>.
    /// </summary>
    /// <remarks>
    /// A literal rather than a reference because this package builds against the published Core;
    /// <c>ArtifactSchemaKeyTests</c> pins it to the canonical value so the two cannot drift.
    /// </remarks>
    internal const string SchemaKey = "tf.artifact.cosmos.item";

    /// <summary>
    /// Identifies this artifact's debug shape, so a consumer renders it as a Cosmos DB item instead of
    /// falling back to a generic view keyed on the CLR type name.
    /// </summary>
    public override string DebugValueSchemaKey => SchemaKey;

    /// <inheritdoc />
    public override JToken? CreateDebugValueCustomPayload(ArtifactInstanceGeneric instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (instance.Reference is not CosmosDbItemArtifactReference<TItem>)
            return null;

        return new JObject
        {
            ["key"] = instance.Identifier.Identifier,
            ["itemType"] = typeof(TItem).Name
        };
    }

    /// <summary>
    /// Removes the referenced item from Cosmos during artifact cleanup.
    /// </summary>
    public override async Task Deconstruct(IServiceProvider serviceProvider, CosmosDbItemArtifactReference<TItem> reference, VariableStore variableStore, ScopedLogger logger)
    {
        CosmosContainerDbConfig config = serviceProvider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig(reference.DbIdentifier);
        ICosmosContainerAdapter container = serviceProvider.GetAzureComponentFactory().Cosmos.CreateContainer(config);

        await container.DeleteItemAsync<TItem>(reference.GetId(variableStore), reference.GetPartitionKey(variableStore));

        logger.LogInformation($"Deleted item from Cosmos DB: {reference.GetId(variableStore)} {reference.GetPartitionKey(variableStore)}");
    }

    /// <summary>
    /// Ensures the referenced Cosmos item exists during artifact setup.
    /// </summary>
    public override async Task Setup(IServiceProvider serviceProvider, CosmosDbItemArtifactData<TItem> data, CosmosDbItemArtifactReference<TItem> reference, VariableStore variableStore, ScopedLogger logger)
    {
        CosmosContainerDbConfig config = serviceProvider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig(reference.DbIdentifier);
        ICosmosContainerAdapter container = serviceProvider.GetAzureComponentFactory().Cosmos.CreateContainer(config);

        ICosmosDbIdentifierResolver resolver = serviceProvider.GetService<ICosmosDbIdentifierResolver>() ?? new DefaultCosmosDbIdentifierResolver();
        reference.SetIdentifier(resolver.ResolveId(data.Item), resolver.ResolvePartitionKey(data.Item));

        logger.LogInformation($"Ensuring Cosmos container exists: {config.DatabaseName}/{config.ContainerName}");
        // ArtifactDescriber.Setup carries no CancellationToken, so the step's real token cannot reach
        // this call yet; the adapter and REST client now accept one for when Core supplies it.
        await container.EnsureContainerExistsAsync(data.Item, CancellationToken.None);

        logger.LogInformation($"Upserting Cosmos item: {reference.GetId(variableStore)} {reference.GetPartitionKey(variableStore)}");
        await container.UpsertItemAsync(data.Item, reference.GetPartitionKey(variableStore));

        logger.LogInformation($"Upserted item to Cosmos DB: {reference.GetId(variableStore)} {reference.GetPartitionKey(variableStore)}");
    }

    /// <summary>
    /// Returns a readable string representation of the describer.
    /// </summary>
    /// <returns>A string representation of the describer.</returns>
    public override string ToString() => $"Cosmos DB Item<{typeof(TItem).Name}>";
}