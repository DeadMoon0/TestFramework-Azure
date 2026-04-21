using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cosmos;
using System;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Runtime;

namespace TestFramework.Azure.Extensions;

/// <summary>
/// Extension methods for configuring Azure services in the dependency injection container.
/// </summary>
public static class AzureServiceCollectionExtension
{
    /// <summary>
    /// Loads Azure configuration settings into the service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection to register Azure configs in.</param>
    /// <param name="configuration">The configuration root from which to load settings.</param>
    /// <param name="provider">Optional custom configuration provider. Uses DefaultConfigProvider if not specified.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection LoadAzureConfigs(this IServiceCollection serviceCollection, IConfiguration configuration, IConfigProvider? provider = null)
    {
        provider ??= new DefaultConfigProvider();
        ConfigLoader loader = new ConfigLoader(provider);
        loader.LoadAllConfigs(configuration, serviceCollection);
        return serviceCollection;
    }

    /// <summary>
    /// Configures how CosmosClientOptions are created for Cosmos-backed triggers and artifacts.
    /// </summary>
    public static IServiceCollection ConfigureCosmosClientOptions(this IServiceCollection serviceCollection, Func<CosmosContainerDbConfig, CosmosClientOptions> configure)
    {
        serviceCollection.AddSingleton<ICosmosClientOptionsProvider>(new DelegateCosmosClientOptionsProvider(configure));
        return serviceCollection;
    }

    private sealed class DelegateCosmosClientOptionsProvider(Func<CosmosContainerDbConfig, CosmosClientOptions> configure) : ICosmosClientOptionsProvider
    {
        public CosmosClientOptions CreateOptions(CosmosContainerDbConfig config) => configure(config);
    }
}