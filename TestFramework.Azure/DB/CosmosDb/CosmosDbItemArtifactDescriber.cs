using TestFramework.Core.Steps;
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
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.DB.CosmosDb;

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
    public override async Task Deconstruct(RunContext context, CosmosDbItemArtifactReference<TItem> reference)
    {
        ICosmosContainerAdapter container = context.Services.GetAzureComponentFactory()
            .Cosmos.CreateContainer(AzureResources.For<CosmosDbConfig>(context, reference.DbIdentifier));

        await container.DeleteItemAsync<TItem>(reference.GetId(context.Variables), reference.GetPartitionKey(context.Variables), context.Deadline.Token);

        context.Logger.LogInformation($"Deleted item from Cosmos DB: {reference.GetId(context.Variables)} {reference.GetPartitionKey(context.Variables)}");
    }

    /// <summary>
    /// Ensures the referenced Cosmos item exists during artifact setup.
    /// </summary>
    public override async Task Setup(RunContext context, CosmosDbItemArtifactData<TItem> data, CosmosDbItemArtifactReference<TItem> reference)
    {
        Resolved<CosmosDbConfig> cosmos = AzureResources.For<CosmosDbConfig>(context, reference.DbIdentifier);
        CosmosDbConfig config = cosmos.Value;
        ICosmosContainerAdapter container = context.Services.GetAzureComponentFactory().Cosmos.CreateContainer(cosmos);

        ICosmosDbIdentifierResolver resolver = context.Services.GetService<ICosmosDbIdentifierResolver>() ?? new DefaultCosmosDbIdentifierResolver();
        string itemId = resolver.ResolveId(data.Item);
        PartitionKey itemPartitionKey = resolver.ResolvePartitionKey(data.Item);

        if (reference.NamesItsItem)
        {
            // Setup writes the item and teardown deletes what the reference names, so the two must be one item.
            string namedId = reference.GetId(context.Variables);
            PartitionKey namedPartitionKey = reference.GetPartitionKey(context.Variables);
            if (!string.Equals(namedId, itemId, StringComparison.Ordinal) || !namedPartitionKey.Equals(itemPartitionKey))
            {
                throw new FrameworkConfigurationException(
                    $"The Cosmos item being set up is '{itemId}' in partition {itemPartitionKey}, and its reference names '{namedId}' in partition {namedPartitionKey}; teardown would delete an item setup never wrote.")
                {
                    RecoverySteps =
                    [
                        "Give the reference the item's own id and partition key.",
                        "Or let setup read them from the item: AddCosmosDbItemArtifact(name, container, item).",
                    ],
                };
            }
        }
        else if (!context.Artifacts.Assign(reference, (held, proof) => held.AssignIdentity(proof, itemId, itemPartitionKey)))
        {
            // Assigned before the write, so an attempt the run stopped waiting for writes nothing it cannot clean up.
            context.Logger.LogWarning($"Cosmos item '{itemId}' was not written: the attempt setting it up is one the run had stopped waiting for.");
            return;
        }

        context.Logger.LogInformation($"Ensuring Cosmos container exists: {config.DatabaseName}/{config.ContainerName}");
        await container.EnsureContainerExistsAsync(data.Item, context.Deadline.Token);

        context.Logger.LogInformation($"Upserting Cosmos item: {reference.GetId(context.Variables)} {reference.GetPartitionKey(context.Variables)}");
        await container.UpsertItemAsync(data.Item, reference.GetPartitionKey(context.Variables), context.Deadline.Token);

        context.Logger.LogInformation($"Upserted item to Cosmos DB: {reference.GetId(context.Variables)} {reference.GetPartitionKey(context.Variables)}");
    }

    /// <summary>
    /// Returns a readable string representation of the describer.
    /// </summary>
    /// <returns>A string representation of the describer.</returns>
    public override string ToString() => $"Cosmos DB Item<{typeof(TItem).Name}>";
}