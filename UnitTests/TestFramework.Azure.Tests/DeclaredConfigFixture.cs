using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Config.Configuration;
using TestFramework.Core.Environment.Graph;

namespace TestFramework.Azure.Tests;

/// <summary>
/// Declares configuration records to a run the way a configuration file would.
/// </summary>
/// <remarks>
/// <para>
/// A fixture used to hand a step its configuration by registering a store, because a store was where a step
/// read from. It is not any more: a run's configuration is run data and travels as the run's resource values,
/// so a store registered in a container reaches nothing.
/// </para>
/// <para>
/// This keeps the convenient half of that idiom - building a record in C# rather than as configuration keys -
/// and sends it down the road the framework actually uses. The store becomes a carrier the fixture fills,
/// read once when the run composes its resources, rather than a channel anything reads at run time.
/// </para>
/// <para>
/// Read lazily on purpose: several tests create a store, add a second entry to it, and register it
/// afterwards. Enumerating at registration would silently drop the second entry.
/// </para>
/// </remarks>
internal static class DeclaredConfigFixture
{
    /// <summary>
    /// Declares everything a store holds, as the run's own resource values.
    /// </summary>
    /// <typeparam name="TConfig">The configuration record.</typeparam>
    /// <param name="services">The service collection the fixture is building.</param>
    /// <param name="store">The records to declare.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection Declare<TConfig>(this IServiceCollection services, TestFramework.Azure.Configuration.ConfigStore<TConfig> store)
        where TConfig : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(store);

        ConfigShape<TConfig> shape = ShapeFor<TConfig>();

        // The shape answers both questions a run has: which kind these records are, and how to turn one back
        // into a record once the values are on the run.
        services.AddSingleton(shape);
        services.AddSingleton<IResourceNodeSource>(new StoreSource<TConfig>(shape, store));

        return services;
    }

    private static ConfigShape<TConfig> ShapeFor<TConfig>()
        where TConfig : class
    {
        DefaultConfigProvider provider = new DefaultConfigProvider();

        object shape = typeof(TConfig) switch
        {
            Type type when type == typeof(StorageAccountConfig) => new StorageAccountShape(provider),
            Type type when type == typeof(ServiceBusConfig) => new ServiceBusShape(provider),
            Type type when type == typeof(CosmosContainerDbConfig) => new CosmosDbShape(provider),
            Type type when type == typeof(FunctionAppConfig) => new FunctionAppShape(provider),
            Type type when type == typeof(LogicAppConfig) => new LogicAppShape(provider),
            Type type when type == typeof(SqlDatabaseConfig) => new SqlDatabaseShape(provider),
            _ => throw new NotSupportedException($"No shape is known for {typeof(TConfig).Name}."),
        };

        return (ConfigShape<TConfig>)shape;
    }

    private sealed class StoreSource<TConfig>(ConfigShape<TConfig> shape, TestFramework.Azure.Configuration.ConfigStore<TConfig> store) : DeclaredNodeSource
        where TConfig : class
    {
        public override string SourceName => "test fixture";

        protected override IEnumerable<DeclaredResource> Declarations
        {
            get
            {
                foreach (KeyValuePair<string, TConfig> entry in store.Snapshot())
                {
                    yield return new DeclaredResource(shape.Kind, entry.Key, shape.Values(entry.Value), this.SourceName);
                }
            }
        }
    }
}
