using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.DB.CosmosDB;

/// <summary>
/// Finds Cosmos DB artifacts by executing a query against the configured container.
/// </summary>
/// <typeparam name="TItem">The item type returned by the query.</typeparam>
/// <param name="dbIdentifier">The Cosmos container identifier.</param>
/// <param name="query">The query definition variable.</param>
public class CosmosDbItemArtifactQueryFinder<TItem>(CosmosContainerIdentifier dbIdentifier, VariableReference<QueryDefinition> query) : ArtifactFinder<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactReference<TItem>>
{
    /// <summary>
    /// Finds the first matching Cosmos item.
    /// </summary>
    public override async Task<ArtifactFinderResult?> FindAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        CosmosContainerDbConfig config = serviceProvider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig(dbIdentifier);
        ICosmosContainerAdapter container = serviceProvider.GetAzureComponentFactory().Cosmos.CreateContainer(config);

        bool found = false;
        TItem? data = default;
        await foreach (TItem item in container.QueryItemsAsync<TItem>(query.GetRequiredValue(variableStore), cancellationToken))
        {
            data = item;
            found = true;
            break;
        }
        if (!found) return null;

        ICosmosDbIdentifierResolver resolver = serviceProvider.GetService<ICosmosDbIdentifierResolver>() ?? new DefaultCosmosDbIdentifierResolver();
        return new ArtifactFinderResult(new CosmosDbItemArtifactReference<TItem>(dbIdentifier, resolver.ResolvePartitionKey(data), resolver.ResolveId(data)));
    }

    /// <summary>
    /// Finds all matching Cosmos items.
    /// </summary>
    public override async Task<ArtifactFinderResultMulti> FindMultiAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        CosmosContainerDbConfig config = serviceProvider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig(dbIdentifier);
        ICosmosContainerAdapter container = serviceProvider.GetAzureComponentFactory().Cosmos.CreateContainer(config);

        ICosmosDbIdentifierResolver resolver = serviceProvider.GetService<ICosmosDbIdentifierResolver>() ?? new DefaultCosmosDbIdentifierResolver();

        List<ArtifactFinderResult> data = [];
        await foreach (TItem item in container.QueryItemsAsync<TItem>(query.GetRequiredValue(variableStore), cancellationToken))
        {
            data.Add(new ArtifactFinderResult(new CosmosDbItemArtifactReference<TItem>(dbIdentifier, resolver.ResolvePartitionKey(item), resolver.ResolveId(item))));
        }

        return new ArtifactFinderResultMulti([.. data]);
    }
}