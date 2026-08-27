using System;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Config.Configuration;

namespace TestFramework.Azure.Configuration;

/// <summary>
/// The shape that owns one of this package's configuration records.
/// </summary>
/// <remarks>
/// <para>
/// A shape is the one place that knows how a record and a run's resource values correspond - which values
/// an entry holds, which of them are optional, and which is a name rather than an address. Anything that
/// needs to declare one of these records as a resource needs that mapping, and writing it a second time
/// is how the two copies come to disagree about a value that is empty rather than absent.
/// </para>
/// <para>
/// Normally the shape arrives through the service collection, registered by the package's loader. This is
/// for the callers that have a record and no run yet - an environment declaring what a definition names,
/// a fixture building an entry in C# - which is why it takes no configuration and no services.
/// </para>
/// </remarks>
public static class AzureConfigShapes
{
    /// <summary>
    /// The shape for one of this package's configuration records.
    /// </summary>
    /// <typeparam name="TConfig">The record.</typeparam>
    /// <returns>Its shape.</returns>
    /// <exception cref="NotSupportedException">The type is not one of this package's records.</exception>
    public static ConfigShape<TConfig> For<TConfig>()
        where TConfig : class
        => (ConfigShape<TConfig>)For(typeof(TConfig));

    /// <summary>
    /// The shape for one of this package's configuration records, named by type.
    /// </summary>
    /// <remarks>
    /// For a caller holding a record it only knows as an <see cref="object"/> - which is what enumerating
    /// several kinds of resource at once leaves you with. <see cref="IConfigShape"/> is enough to declare
    /// one: it names the kind and maps the record.
    /// </remarks>
    /// <param name="configType">The record's type.</param>
    /// <returns>Its shape.</returns>
    /// <exception cref="NotSupportedException">The type is not one of this package's records.</exception>
    public static IConfigShape For(Type configType)
    {
        ArgumentNullException.ThrowIfNull(configType);

        DefaultConfigProvider provider = new DefaultConfigProvider();

        return configType switch
        {
            Type type when type == typeof(StorageAccountConfig) => new StorageAccountShape(provider),
            Type type when type == typeof(CosmosContainerDbConfig) => new CosmosDbShape(provider),
            Type type when type == typeof(SqlDatabaseConfig) => new SqlDatabaseShape(provider),
            Type type when type == typeof(ServiceBusConfig) => new ServiceBusShape(provider),
            Type type when type == typeof(FunctionAppConfig) => new FunctionAppShape(provider),
            Type type when type == typeof(LogicAppConfig) => new LogicAppShape(provider),
            _ => throw new NotSupportedException($"{configType.Name} is not one of the Azure package's configuration records."),
        };
    }
}
