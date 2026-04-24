using TestFramework.Azure.Configuration.SpecificConfigs;

namespace TestFramework.Azure.Configuration;

public sealed class DefaultConfigExporter : IConfigExporter
{
    public IReadOnlyDictionary<string, string> ExportFunctionAppConfig(string identifier, FunctionAppConfig config) =>
        CreateSection(
            DefaultConfigProvider.FunctionAppBaseSelector,
            identifier,
            (nameof(FunctionAppConfig.BaseUrl), config.BaseUrl),
            (nameof(FunctionAppConfig.Code), config.Code),
            (nameof(FunctionAppConfig.AdminCode), config.AdminCode));

    public IReadOnlyDictionary<string, string> ExportStorageAccountConfig(string identifier, StorageAccountConfig config) =>
        CreateSection(
            DefaultConfigProvider.StorageAccountSelector,
            identifier,
            (nameof(StorageAccountConfig.ConnectionString), config.ConnectionString),
            (nameof(StorageAccountConfig.QueueContainerName), config.QueueContainerName),
            (nameof(StorageAccountConfig.BlobContainerName), config.BlobContainerName),
            (nameof(StorageAccountConfig.TableContainerName), config.TableContainerName));

    public IReadOnlyDictionary<string, string> ExportCosmosDbConfig(string identifier, CosmosContainerDbConfig config) =>
        CreateSection(
            DefaultConfigProvider.CosmosDbSelector,
            identifier,
            (nameof(CosmosContainerDbConfig.ConnectionString), config.ConnectionString),
            (nameof(CosmosContainerDbConfig.DatabaseName), config.DatabaseName),
            (nameof(CosmosContainerDbConfig.ContainerName), config.ContainerName));

    public IReadOnlyDictionary<string, string> ExportServiceBusConfig(string identifier, ServiceBusConfig config) =>
        CreateSection(
            DefaultConfigProvider.ServiceBusSelector,
            identifier,
            (nameof(ServiceBusConfig.ConnectionString), config.ConnectionString),
            (nameof(ServiceBusConfig.QueueName), config.QueueName),
            (nameof(ServiceBusConfig.TopicName), config.TopicName),
            (nameof(ServiceBusConfig.SubscriptionName), config.SubscriptionName),
            (nameof(ServiceBusConfig.RequiredSession), config.RequiredSession.ToString()));

    public IReadOnlyDictionary<string, string> ExportSqlDatabaseConfig(string identifier, SqlDatabaseConfig config) =>
        CreateSection(
            DefaultConfigProvider.SqlDatabaseSelector,
            identifier,
            (nameof(SqlDatabaseConfig.ConnectionString), config.ConnectionString),
            (nameof(SqlDatabaseConfig.DatabaseName), config.DatabaseName),
            (nameof(SqlDatabaseConfig.ContextType), config.ContextType));

    private static IReadOnlyDictionary<string, string> CreateSection(string selector, string identifier, params (string PropertyName, string? Value)[] values)
    {
        Dictionary<string, string> exported = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string propertyName, string? value) in values)
        {
            if (value is null)
                continue;

            exported[$"{selector}:{identifier}:{propertyName}"] = value;
        }

        return exported;
    }
}