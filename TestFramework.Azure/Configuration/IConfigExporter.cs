using TestFramework.Azure.Configuration.SpecificConfigs;

namespace TestFramework.Azure.Configuration;

/// <summary>
/// Converts strongly typed Azure configuration records into flat configuration key/value pairs.
/// </summary>
public interface IConfigExporter
{
    /// <summary>
    /// Exports a Logic App configuration record.
    /// </summary>
    /// <param name="identifier">The logical identifier to export under.</param>
    /// <param name="config">The configuration record to flatten.</param>
    /// <returns>A key/value map suitable for configuration providers such as in-memory collections.</returns>
    IReadOnlyDictionary<string, string> ExportLogicAppConfig(string identifier, LogicAppConfig config);

    /// <summary>
    /// Exports a Function App configuration record.
    /// </summary>
    /// <param name="identifier">The logical identifier to export under.</param>
    /// <param name="config">The configuration record to flatten.</param>
    /// <returns>A key/value map suitable for configuration providers such as in-memory collections.</returns>
    IReadOnlyDictionary<string, string> ExportFunctionAppConfig(string identifier, FunctionAppConfig config);

    /// <summary>
    /// Exports a Storage Account configuration record.
    /// </summary>
    /// <param name="identifier">The logical identifier to export under.</param>
    /// <param name="config">The configuration record to flatten.</param>
    /// <returns>A key/value map suitable for configuration providers such as in-memory collections.</returns>
    IReadOnlyDictionary<string, string> ExportStorageAccountConfig(string identifier, StorageAccountConfig config);

    /// <summary>
    /// Exports a Cosmos DB configuration record.
    /// </summary>
    /// <param name="identifier">The logical identifier to export under.</param>
    /// <param name="config">The configuration record to flatten.</param>
    /// <returns>A key/value map suitable for configuration providers such as in-memory collections.</returns>
    IReadOnlyDictionary<string, string> ExportCosmosDbConfig(string identifier, CosmosContainerDbConfig config);

    /// <summary>
    /// Exports a Service Bus configuration record.
    /// </summary>
    /// <param name="identifier">The logical identifier to export under.</param>
    /// <param name="config">The configuration record to flatten.</param>
    /// <returns>A key/value map suitable for configuration providers such as in-memory collections.</returns>
    IReadOnlyDictionary<string, string> ExportServiceBusConfig(string identifier, ServiceBusConfig config);

    /// <summary>
    /// Exports a SQL database configuration record.
    /// </summary>
    /// <param name="identifier">The logical identifier to export under.</param>
    /// <param name="config">The configuration record to flatten.</param>
    /// <returns>A key/value map suitable for configuration providers such as in-memory collections.</returns>
    IReadOnlyDictionary<string, string> ExportSqlDatabaseConfig(string identifier, SqlDatabaseConfig config);
}