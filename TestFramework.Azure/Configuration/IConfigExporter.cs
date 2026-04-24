using TestFramework.Azure.Configuration.SpecificConfigs;

namespace TestFramework.Azure.Configuration;

public interface IConfigExporter
{
    IReadOnlyDictionary<string, string> ExportFunctionAppConfig(string identifier, FunctionAppConfig config);
    IReadOnlyDictionary<string, string> ExportStorageAccountConfig(string identifier, StorageAccountConfig config);
    IReadOnlyDictionary<string, string> ExportCosmosDbConfig(string identifier, CosmosContainerDbConfig config);
    IReadOnlyDictionary<string, string> ExportServiceBusConfig(string identifier, ServiceBusConfig config);
    IReadOnlyDictionary<string, string> ExportSqlDatabaseConfig(string identifier, SqlDatabaseConfig config);
}