using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.DB.CosmosDB;

public class CosmosDbItemArtifactDescriber<TItem> : ArtifactDescriber<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactReference<TItem>>
{
    public override async Task Deconstruct(IServiceProvider serviceProvider, CosmosDbItemArtifactReference<TItem> reference, VariableStore variableStore, ScopedLogger logger)
    {
        CosmosContainerDbConfig config = serviceProvider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig(reference.DbIdentifier);
        CosmosClient client = new CosmosClient(config.ConnectionString);
        Database database = client.GetDatabase(config.DatabaseName);
        Container container = database.GetContainer(config.ContainerName);

        await container.DeleteItemAsync<TItem>(reference.GetId(variableStore), reference.GetPartitionKey(variableStore));

        logger.LogInformation($"Deleted item from Cosmos DB: {reference.GetId(variableStore)} {reference.GetPartitionKey(variableStore)}");
    }

    public override async Task Setup(IServiceProvider serviceProvider, CosmosDbItemArtifactData<TItem> data, CosmosDbItemArtifactReference<TItem> reference, VariableStore variableStore, ScopedLogger logger)
    {
        CosmosContainerDbConfig config = serviceProvider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig(reference.DbIdentifier);
        CosmosClient client = new CosmosClient(config.ConnectionString);
        Database database = client.GetDatabase(config.DatabaseName);
        Container container = database.GetContainer(config.ContainerName);

        ICosmosDbIdentifierResolver resolver = serviceProvider.GetService<ICosmosDbIdentifierResolver>() ?? new DefaultCosmosDbIdentifierResolver();
        reference.SetIdentifier(resolver.ResolveId(data.Item), resolver.ResolvePartitionKey(data.Item));

        var itemResp = await container.UpsertItemAsync(data.Item, reference.GetPartitionKey(variableStore));

        logger.LogInformation($"Upserted item to Cosmos DB: {reference.GetId(variableStore)} {reference.GetPartitionKey(variableStore)}");
    }

    public override string ToString() => $"Cosmos DB Item<{typeof(TItem).Name}>";
}