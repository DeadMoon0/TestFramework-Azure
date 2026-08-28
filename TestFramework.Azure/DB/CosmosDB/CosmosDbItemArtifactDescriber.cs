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
    public override async Task Deconstruct(RunContext context, CosmosDbItemArtifactReference<TItem> reference)
    {
        ICosmosContainerAdapter container = context.Services.GetAzureComponentFactory()
            .Cosmos.CreateContainer(AzureResources.For<CosmosContainerDbConfig>(context, reference.DbIdentifier));

        await container.DeleteItemAsync<TItem>(reference.GetId(context.Variables), reference.GetPartitionKey(context.Variables), context.Deadline.Token);

        context.Logger.LogInformation($"Deleted item from Cosmos DB: {reference.GetId(context.Variables)} {reference.GetPartitionKey(context.Variables)}");
    }

    /// <summary>
    /// Ensures the referenced Cosmos item exists during artifact setup.
    /// </summary>
    public override async Task Setup(RunContext context, CosmosDbItemArtifactData<TItem> data, CosmosDbItemArtifactReference<TItem> reference)
    {
        Resolved<CosmosContainerDbConfig> cosmos = AzureResources.For<CosmosContainerDbConfig>(context, reference.DbIdentifier);
        CosmosContainerDbConfig config = cosmos.Value;
        ICosmosContainerAdapter container = context.Services.GetAzureComponentFactory().Cosmos.CreateContainer(cosmos);

        ICosmosDbIdentifierResolver resolver = context.Services.GetService<ICosmosDbIdentifierResolver>() ?? new DefaultCosmosDbIdentifierResolver();
        reference.SetIdentifier(resolver.ResolveId(data.Item), resolver.ResolvePartitionKey(data.Item));

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