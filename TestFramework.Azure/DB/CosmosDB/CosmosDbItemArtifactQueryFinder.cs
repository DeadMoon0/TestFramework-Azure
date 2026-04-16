using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.DB.CosmosDB;

public class CosmosDbItemArtifactQueryFinder<TItem>(CosmosContainerIdentifier dbIdentifier, VariableReference<QueryDefinition> query) : ArtifactFinder<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactReference<TItem>>
{
    public override async Task<ArtifactFinderResult?> FindAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        CosmosContainerDbConfig config = serviceProvider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig(dbIdentifier);
        CosmosClient client = new CosmosClient(config.ConnectionString);
        Database database = client.GetDatabase(config.DatabaseName);
        Container container = database.GetContainer(config.ContainerName);

        FeedIterator<TItem> itemResp = container.GetItemQueryIterator<TItem>(query.GetRequiredValue(variableStore));
        bool found = false;
        TItem? data = default;
        while (itemResp.HasMoreResults)
        {
            foreach (var item in await itemResp.ReadNextAsync(cancellationToken))
            {
                data = item;
                found = true;
                break;
            }
        }
        if (!found) return null;

        ICosmosDbIdentifierResolver resolver = serviceProvider.GetService<ICosmosDbIdentifierResolver>() ?? new DefaultCosmosDbIdentifierResolver();
        return new ArtifactFinderResult(new CosmosDbItemArtifactReference<TItem>(dbIdentifier, resolver.ResolvePartitionKey(data), resolver.ResolveId(data)));
    }

    public override async Task<ArtifactFinderResultMulti> FindMultiAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        CosmosContainerDbConfig config = serviceProvider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig(dbIdentifier);
        CosmosClient client = new CosmosClient(config.ConnectionString);
        Database database = client.GetDatabase(config.DatabaseName);
        Container container = database.GetContainer(config.ContainerName);

        ICosmosDbIdentifierResolver resolver = serviceProvider.GetService<ICosmosDbIdentifierResolver>() ?? new DefaultCosmosDbIdentifierResolver();

        FeedIterator<TItem> itemResp = container.GetItemQueryIterator<TItem>(query.GetRequiredValue(variableStore));
        List<ArtifactFinderResult> data = [];
        while (itemResp.HasMoreResults)
        {
            foreach (var item in await itemResp.ReadNextAsync(cancellationToken))
            {
                data.Add(new ArtifactFinderResult(new CosmosDbItemArtifactReference<TItem>(dbIdentifier, resolver.ResolvePartitionKey(item), resolver.ResolveId(item))));
            }
        }

        return new ArtifactFinderResultMulti([.. data]);
    }
}