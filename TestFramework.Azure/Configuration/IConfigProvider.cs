using Microsoft.Extensions.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;

namespace TestFramework.Azure.Configuration;

/// <summary>
/// Loads Azure resource configuration records from an <see cref="IConfiguration"/> tree.
/// </summary>
/// <remarks>
/// The default implementation expects top-level sections named after the resource type, for example
/// <c>FunctionApp</c>, <c>CosmosDb</c>, or <c>ServiceBus</c>.
/// </remarks>
public interface IConfigProvider
{
    /// <summary>
    /// Returns every configured Logic App identifier.
    /// </summary>
    /// <param name="configuration">The configuration source to inspect.</param>
    /// <returns>All configured Logic App identifiers.</returns>
    public string[] LoadAllLogicAppIdentifier(IConfiguration configuration);

    /// <summary>
    /// Loads the Logic App configuration for a specific identifier.
    /// </summary>
    /// <param name="configuration">The configuration source to inspect.</param>
    /// <param name="identifier">The logical Logic App identifier used by the Azure DSL.</param>
    /// <returns>The resolved Logic App configuration.</returns>
    public LogicAppConfig LoadLogicAppConfig(IConfiguration configuration, string identifier);

    /// <summary>
    /// Returns every configured Function App identifier.
    /// </summary>
    /// <param name="configuration">The configuration source to inspect.</param>
    /// <returns>All configured Function App identifiers.</returns>
    public string[] LoadAllFunctionAppIdentifier(IConfiguration configuration);

    /// <summary>
    /// Loads the Function App configuration for a specific identifier.
    /// </summary>
    /// <param name="configuration">The configuration source to inspect.</param>
    /// <param name="identifier">The logical Function App identifier used by the Azure DSL.</param>
    /// <returns>The resolved Function App configuration.</returns>
    public FunctionAppConfig LoadFunctionAppConfig(IConfiguration configuration, string identifier);

    /// <summary>
    /// Returns every configured Storage Account identifier.
    /// </summary>
    /// <param name="configuration">The configuration source to inspect.</param>
    /// <returns>All configured Storage Account identifiers.</returns>
    public string[] LoadAllStorageAccountIdentifier(IConfiguration configuration);

    /// <summary>
    /// Loads the Storage Account configuration for a specific identifier.
    /// </summary>
    /// <param name="configuration">The configuration source to inspect.</param>
    /// <param name="identifier">The logical Storage Account identifier used by the Azure DSL.</param>
    /// <returns>The resolved Storage Account configuration.</returns>
    public StorageAccountConfig LoadStorageAccountConfig(IConfiguration configuration, string identifier);

    /// <summary>
    /// Returns every configured Cosmos DB container identifier.
    /// </summary>
    /// <param name="configuration">The configuration source to inspect.</param>
    /// <returns>All configured Cosmos DB container identifiers.</returns>
    public string[] LoadAllCosmosDbIdentifier(IConfiguration configuration);

    /// <summary>
    /// Loads the Cosmos DB container configuration for a specific identifier.
    /// </summary>
    /// <param name="configuration">The configuration source to inspect.</param>
    /// <param name="identifier">The logical Cosmos DB identifier used by the Azure DSL.</param>
    /// <returns>The resolved Cosmos DB container configuration.</returns>
    public CosmosContainerDbConfig LoadCosmosDbConfig(IConfiguration configuration, string identifier);

    /// <summary>
    /// Returns every configured Service Bus identifier.
    /// </summary>
    /// <param name="configuration">The configuration source to inspect.</param>
    /// <returns>All configured Service Bus identifiers.</returns>
    public string[] LoadAllServiceBusIdentifier(IConfiguration configuration);

    /// <summary>
    /// Loads the Service Bus configuration for a specific identifier.
    /// </summary>
    /// <param name="configuration">The configuration source to inspect.</param>
    /// <param name="identifier">The logical Service Bus identifier used by the Azure DSL.</param>
    /// <returns>The resolved Service Bus configuration.</returns>
    public ServiceBusConfig LoadServiceBusConfig(IConfiguration configuration, string identifier);

    /// <summary>
    /// Returns every configured SQL database identifier.
    /// </summary>
    /// <param name="configuration">The configuration source to inspect.</param>
    /// <returns>All configured SQL database identifiers.</returns>
    public string[] LoadAllSqlDatabaseIdentifier(IConfiguration configuration);

    /// <summary>
    /// Loads the SQL database configuration for a specific identifier.
    /// </summary>
    /// <param name="configuration">The configuration source to inspect.</param>
    /// <param name="identifier">The logical SQL database identifier used by the Azure DSL.</param>
    /// <returns>The resolved SQL database configuration.</returns>
    public SqlDatabaseConfig LoadSqlDatabaseConfig(IConfiguration configuration, string identifier);
}