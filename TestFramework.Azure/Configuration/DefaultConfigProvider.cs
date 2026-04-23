using Microsoft.Extensions.Configuration;
using System.Linq;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Exceptions;

namespace TestFramework.Azure.Configuration;

public class DefaultConfigProvider : IConfigProvider
{
    public const string FunctionAppBaseSelector = "FunctionApp";
    public const string StorageAccountSelector = "StorageAccount";
    public const string CosmosDbSelector = "CosmosDb";
    public const string ServiceBusSelector = "ServiceBus";
    public const string SqlDatabaseSelector = "SqlDatabase";

    public string[] LoadAllCosmosDbIdentifier(IConfiguration configuration) => [.. configuration.GetSection(CosmosDbSelector).GetChildren().Select(x => x.Key)];

    public string[] LoadAllFunctionAppIdentifier(IConfiguration configuration) => [.. configuration.GetSection(FunctionAppBaseSelector).GetChildren().Select(x => x.Key)];

    public string[] LoadAllStorageAccountIdentifier(IConfiguration configuration) => [.. configuration.GetSection(StorageAccountSelector).GetChildren().Select(x => x.Key)];

    public string[] LoadAllSqlDatabaseIdentifier(IConfiguration configuration) => [.. configuration.GetSection(SqlDatabaseSelector).GetChildren().Select(x => x.Key)];

    public string[] LoadAllServiceBusIdentifier(IConfiguration configuration) => [.. configuration.GetSection(ServiceBusSelector).GetChildren().Select(x => x.Key)];

    public CosmosContainerDbConfig LoadCosmosDbConfig(IConfiguration configuration, string identifier)
    {
        return new CosmosContainerDbConfig
        {
            ConnectionString = configuration.GetSection(CosmosDbSelector).GetSection(identifier).GetSection(nameof(CosmosContainerDbConfig.ConnectionString)).Value ?? throw new ConfigurationValidationException(nameof(CosmosContainerDbConfig.ConnectionString)),
            DatabaseName = configuration.GetSection(CosmosDbSelector).GetSection(identifier).GetSection(nameof(CosmosContainerDbConfig.DatabaseName)).Value ?? throw new ConfigurationValidationException(nameof(CosmosContainerDbConfig.DatabaseName)),
            ContainerName = configuration.GetSection(CosmosDbSelector).GetSection(identifier).GetSection(nameof(CosmosContainerDbConfig.ContainerName)).Value ?? throw new ConfigurationValidationException(nameof(CosmosContainerDbConfig.ContainerName)),
            PartitionKeyPath = configuration.GetSection(CosmosDbSelector).GetSection(identifier).GetSection(nameof(CosmosContainerDbConfig.PartitionKeyPath)).Value,
        };
    }

    public ServiceBusConfig LoadServiceBusConfig(IConfiguration configuration, string identifier)
    {
        return new ServiceBusConfig
        {
            ConnectionString = configuration.GetSection(ServiceBusSelector).GetSection(identifier).GetSection(nameof(ServiceBusConfig.ConnectionString)).Value ?? throw new ConfigurationValidationException(nameof(ServiceBusConfig.ConnectionString)),
            QueueName = configuration.GetSection(ServiceBusSelector).GetSection(identifier).GetSection(nameof(ServiceBusConfig.QueueName)).Value,
            TopicName = configuration.GetSection(ServiceBusSelector).GetSection(identifier).GetSection(nameof(ServiceBusConfig.TopicName)).Value,
            SubscriptionName = configuration.GetSection(ServiceBusSelector).GetSection(identifier).GetSection(nameof(ServiceBusConfig.SubscriptionName)).Value,
            RequiredSession = bool.Parse(configuration.GetSection(ServiceBusSelector).GetSection(identifier).GetSection(nameof(ServiceBusConfig.RequiredSession)).Value ?? bool.FalseString),
        };
    }

    public FunctionAppConfig LoadFunctionAppConfig(IConfiguration configuration, string identifier)
    {
        return new FunctionAppConfig
        {
            BaseUrl = configuration.GetSection(FunctionAppBaseSelector).GetSection(identifier).GetSection(nameof(FunctionAppConfig.BaseUrl)).Value ?? throw new ConfigurationValidationException(nameof(FunctionAppConfig.BaseUrl)),
            Code = configuration.GetSection(FunctionAppBaseSelector).GetSection(identifier).GetSection(nameof(FunctionAppConfig.Code)).Value ?? throw new ConfigurationValidationException(nameof(FunctionAppConfig.Code)),
            AdminCode = configuration.GetSection(FunctionAppBaseSelector).GetSection(identifier).GetSection(nameof(FunctionAppConfig.AdminCode)).Value,
        };
    }

    public StorageAccountConfig LoadStorageAccountConfig(IConfiguration configuration, string identifier)
    {
        return new StorageAccountConfig
        {
            ConnectionString = configuration.GetSection(StorageAccountSelector).GetSection(identifier).GetSection(nameof(StorageAccountConfig.ConnectionString)).Value ?? throw new ConfigurationValidationException(nameof(StorageAccountConfig.ConnectionString)),
            BlobContainerName = configuration.GetSection(StorageAccountSelector).GetSection(identifier).GetSection(nameof(StorageAccountConfig.BlobContainerName)).Value,
            QueueContainerName = configuration.GetSection(StorageAccountSelector).GetSection(identifier).GetSection(nameof(StorageAccountConfig.QueueContainerName)).Value,
            TableContainerName = configuration.GetSection(StorageAccountSelector).GetSection(identifier).GetSection(nameof(StorageAccountConfig.TableContainerName)).Value,
        };
    }

    public SqlDatabaseConfig LoadSqlDatabaseConfig(IConfiguration configuration, string identifier)
    {
        return new SqlDatabaseConfig
        {
            ConnectionString = configuration.GetSection(SqlDatabaseSelector).GetSection(identifier).GetSection(nameof(SqlDatabaseConfig.ConnectionString)).Value ?? throw new ConfigurationValidationException(nameof(SqlDatabaseConfig.ConnectionString)),
            DatabaseName = configuration.GetSection(SqlDatabaseSelector).GetSection(identifier).GetSection(nameof(SqlDatabaseConfig.DatabaseName)).Value ?? throw new ConfigurationValidationException(nameof(SqlDatabaseConfig.DatabaseName)),
            ContextType = configuration.GetSection(SqlDatabaseSelector).GetSection(identifier).GetSection(nameof(SqlDatabaseConfig.ContextType)).Value,
        };
    }
}