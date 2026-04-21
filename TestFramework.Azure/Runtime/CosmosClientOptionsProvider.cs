using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using System;
using TestFramework.Azure.Configuration.SpecificConfigs;

namespace TestFramework.Azure.Runtime;

public interface ICosmosClientOptionsProvider
{
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