using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using System;
using TestFramework.Azure.Configuration.SpecificConfigs;

namespace TestFramework.Azure.Runtime;

/// <summary>
/// Produces <see cref="CosmosClientOptions"/> for Cosmos-backed Azure operations.
/// </summary>
public interface ICosmosClientOptionsProvider
{
    /// <summary>
    /// Creates client options for a resolved Cosmos configuration.
    /// </summary>
    /// <param name="config">The resolved Cosmos DB configuration.</param>
    /// <returns>The Cosmos client options to use.</returns>
    CosmosClientOptions CreateOptions(CosmosContainerDbConfig config);
}

internal static class CosmosClientOptionsResolver
{
    internal static CosmosClientOptions Resolve(IServiceProvider serviceProvider, CosmosContainerDbConfig config)
    {
        return serviceProvider.GetService<ICosmosClientOptionsProvider>()?.CreateOptions(config)
            ?? new CosmosClientOptions();
    }
}