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
    /// Turns this package's configuration on: which sections it reads, and how each one maps to a resource.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Takes no configuration, deliberately.</strong> It used to, and the parameter stopped meaning
    /// anything when the typed stores were deleted: a shape reads the configuration the run holds, when the
    /// run composes its resources. Keeping an ignored parameter would have let a caller pass one
    /// configuration here, register another on the run, and be served the second without a word.
    /// </para>
    /// <para>
    /// So the run decides which configuration these sections are read from, which is the same rule every
    /// other package follows and the reason a step never has to ask which of two files won.
    /// </para>
    /// </remarks>
    /// <param name="serviceCollection">The service collection to register Azure configs in.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection LoadAzureConfigs(this IServiceCollection serviceCollection)
        => serviceCollection.LoadAzureConfigs(null);

    /// <summary>
    /// Turns this package's configuration on: which sections it reads, and how each one maps to a resource.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Takes no configuration, deliberately.</strong> It used to, and the parameter stopped meaning
    /// anything when the typed stores were deleted: a shape reads the configuration the run holds, when the
    /// run composes its resources. Keeping an ignored parameter would have let a caller pass one
    /// configuration here, register another on the run, and be served the second without a word.
    /// </para>
    /// <para>
    /// So the run decides which configuration these sections are read from, which is the same rule every
    /// other package follows and the reason a step never has to ask which of two files won.
    /// </para>
    /// </remarks>
    /// <param name="serviceCollection">The service collection to register Azure configs in.</param>
    /// <param name="provider">Optional custom configuration provider. Uses DefaultConfigProvider if not specified.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection LoadAzureConfigs(this IServiceCollection serviceCollection, IConfigProvider? provider)
    {
        provider ??= new DefaultConfigProvider();
        ConfigLoader loader = new ConfigLoader(provider);
        loader.RegisterShapes(serviceCollection);

        // Azure SDK clients are connection multiplexers, so they are created once and owned by the
        // container, which disposes them when the service provider goes away.
        serviceCollection.AddSingleton<AzureClientCache>();
        return serviceCollection;
    }

    /// <summary>
    /// Configures how CosmosClientOptions are created for Cosmos-backed triggers and artifacts.
    /// </summary>
    /// <param name="serviceCollection">The service collection to register the options provider in.</param>
    /// <param name="configure">A delegate that creates client options for each resolved <see cref="CosmosContainerDbConfig"/>.</param>
    /// <returns>The service collection for fluent chaining.</returns>
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