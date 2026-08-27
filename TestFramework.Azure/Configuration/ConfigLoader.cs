using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Config.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;

namespace TestFramework.Azure.Configuration;

/// <summary>
/// Registers the shapes that describe this package's configuration sections.
/// </summary>
/// <remarks>
/// <para>
/// This used to be six methods that each did two things: fill a typed store from a configuration, and
/// register a shape. Only the shape was ever read, so the stores were deleted - and with them the reason
/// this class ever took an <see cref="IConfiguration"/>. A shape reads the configuration the run holds,
/// when the run composes its resources.
/// </para>
/// <para>
/// Losing the parameter matters more than losing the lines. While it was still there and no longer used,
/// a caller could hand this one configuration, register a different one on the run, and be quietly served
/// the second - which is exactly what one of this package's own tests did, and it is how the change was
/// noticed.
/// </para>
/// </remarks>
internal class ConfigLoader(IConfigProvider configProvider)
{
    internal void RegisterShapes(IServiceCollection serviceCollection)
    {
        serviceCollection.AddConfigShape(new LogicAppShape(configProvider));
        serviceCollection.AddConfigShape(new FunctionAppShape(configProvider));
        serviceCollection.AddConfigShape(new CosmosDbShape(configProvider));
        serviceCollection.AddConfigShape(new ServiceBusShape(configProvider));
        serviceCollection.AddConfigShape(new StorageAccountShape(configProvider));
        serviceCollection.AddConfigShape(new SqlDatabaseShape(configProvider));
    }
}
