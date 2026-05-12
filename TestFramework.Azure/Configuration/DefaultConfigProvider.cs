using System;
using Microsoft.Extensions.Configuration;
using System.Linq;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Exceptions;
using TestFramework.Azure.LogicApp;

namespace TestFramework.Azure.Configuration;

/// <summary>
/// Default <see cref="IConfigProvider"/> implementation that reads Azure settings from standard named sections.
/// </summary>
public class DefaultConfigProvider : IConfigProvider
{
    /// <summary>
    /// Configuration section name for <see cref="LogicAppConfig"/> records.
    /// </summary>
    public const string LogicAppSelector = "LogicApp";

    /// <summary>
    /// Configuration section name for <see cref="FunctionAppConfig"/> records.
    /// </summary>
    public const string FunctionAppBaseSelector = "FunctionApp";

    /// <summary>
    /// Configuration section name for <see cref="StorageAccountConfig"/> records.
    /// </summary>
    public const string StorageAccountSelector = "StorageAccount";

    /// <summary>
    /// Configuration section name for <see cref="CosmosContainerDbConfig"/> records.
    /// </summary>
    public const string CosmosDbSelector = "CosmosDb";

    /// <summary>
    /// Configuration section name for <see cref="ServiceBusConfig"/> records.
    /// </summary>
    public const string ServiceBusSelector = "ServiceBus";

    /// <summary>
    /// Configuration section name for <see cref="SqlDatabaseConfig"/> records.
    /// </summary>
    public const string SqlDatabaseSelector = "SqlDatabase";

    /// <inheritdoc />
    public string[] LoadAllLogicAppIdentifier(IConfiguration configuration) => [.. configuration.GetSection(LogicAppSelector).GetChildren().Select(x => x.Key)];

    /// <inheritdoc />
    public string[] LoadAllCosmosDbIdentifier(IConfiguration configuration) => [.. configuration.GetSection(CosmosDbSelector).GetChildren().Select(x => x.Key)];

    /// <inheritdoc />
    public string[] LoadAllFunctionAppIdentifier(IConfiguration configuration) => [.. configuration.GetSection(FunctionAppBaseSelector).GetChildren().Select(x => x.Key)];

    /// <inheritdoc />
    public string[] LoadAllStorageAccountIdentifier(IConfiguration configuration) => [.. configuration.GetSection(StorageAccountSelector).GetChildren().Select(x => x.Key)];

    /// <inheritdoc />
    public string[] LoadAllSqlDatabaseIdentifier(IConfiguration configuration) => [.. configuration.GetSection(SqlDatabaseSelector).GetChildren().Select(x => x.Key)];

    /// <inheritdoc />
    public string[] LoadAllServiceBusIdentifier(IConfiguration configuration) => [.. configuration.GetSection(ServiceBusSelector).GetChildren().Select(x => x.Key)];

    /// <inheritdoc />
    public LogicAppConfig LoadLogicAppConfig(IConfiguration configuration, string identifier)
    {
        IConfigurationSection logicAppSection = configuration.GetSection(LogicAppSelector).GetSection(identifier);
        IConfigurationSection standardSection = logicAppSection.GetSection(nameof(LogicAppConfig.Standard));
        IConfigurationSection consumptionSection = logicAppSection.GetSection(nameof(LogicAppConfig.Consumption));

        return new LogicAppConfig
        {
            HostingMode = Enum.TryParse<LogicAppHostingMode>(logicAppSection.GetSection(nameof(LogicAppConfig.HostingMode)).Value, ignoreCase: true, out LogicAppHostingMode mode) ? mode : LogicAppHostingMode.Standard,
            WorkflowName = logicAppSection.GetSection(nameof(LogicAppConfig.WorkflowName)).Value,
            Standard = new LogicAppStandardConfig
            {
                BaseUrl = standardSection.GetSection(nameof(LogicAppStandardConfig.BaseUrl)).Value,
                Code = standardSection.GetSection(nameof(LogicAppStandardConfig.Code)).Value,
                AdminCode = standardSection.GetSection(nameof(LogicAppStandardConfig.AdminCode)).Value,
            },
            Consumption = new LogicAppConsumptionConfig
            {
                InvokeUrl = consumptionSection.GetSection(nameof(LogicAppConsumptionConfig.InvokeUrl)).Value,
                WorkflowResourceId = consumptionSection.GetSection(nameof(LogicAppConsumptionConfig.WorkflowResourceId)).Value,
            },
        };
    }

    /// <inheritdoc />
    public CosmosContainerDbConfig LoadCosmosDbConfig(IConfiguration configuration, string identifier)
    {
        return new CosmosContainerDbConfig
        {
            ConnectionString = configuration.GetSection(CosmosDbSelector).GetSection(identifier).GetSection(nameof(CosmosContainerDbConfig.ConnectionString)).Value ?? throw new ConfigurationValidationException(nameof(CosmosContainerDbConfig.ConnectionString)),
            DatabaseName = configuration.GetSection(CosmosDbSelector).GetSection(identifier).GetSection(nameof(CosmosContainerDbConfig.DatabaseName)).Value ?? throw new ConfigurationValidationException(nameof(CosmosContainerDbConfig.DatabaseName)),
            ContainerName = configuration.GetSection(CosmosDbSelector).GetSection(identifier).GetSection(nameof(CosmosContainerDbConfig.ContainerName)).Value ?? throw new ConfigurationValidationException(nameof(CosmosContainerDbConfig.ContainerName)),
        };
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public FunctionAppConfig LoadFunctionAppConfig(IConfiguration configuration, string identifier)
    {
        return new FunctionAppConfig
        {
            BaseUrl = configuration.GetSection(FunctionAppBaseSelector).GetSection(identifier).GetSection(nameof(FunctionAppConfig.BaseUrl)).Value ?? throw new ConfigurationValidationException(nameof(FunctionAppConfig.BaseUrl)),
            Code = configuration.GetSection(FunctionAppBaseSelector).GetSection(identifier).GetSection(nameof(FunctionAppConfig.Code)).Value ?? throw new ConfigurationValidationException(nameof(FunctionAppConfig.Code)),
            AdminCode = configuration.GetSection(FunctionAppBaseSelector).GetSection(identifier).GetSection(nameof(FunctionAppConfig.AdminCode)).Value,
        };
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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