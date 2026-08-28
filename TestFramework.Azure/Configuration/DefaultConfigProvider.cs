using System;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Linq;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Exceptions;
using TestFramework.Azure.LogicApp;
using TestFramework.Core.Exceptions;

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

        // A typo refuses with the option list instead of silently falling back to Standard - the
        // values-path reader already refuses this way, and two readers for one section must agree.
        string? declaredMode = logicAppSection.GetSection(nameof(LogicAppConfig.HostingMode)).Value;
        LogicAppHostingMode hostingMode = LogicAppHostingMode.Standard;
        if (declaredMode is { Length: > 0 })
        {
            hostingMode = Enum.TryParse(declaredMode, ignoreCase: true, out LogicAppHostingMode parsed)
                ? parsed
                : throw new FrameworkConfigurationException(
                    $"'{LogicAppSelector}:{identifier}:{nameof(LogicAppConfig.HostingMode)}' holds '{declaredMode}', which is not a hosting mode.",
                    [$"Use one of: {string.Join(", ", Enum.GetNames<LogicAppHostingMode>())}."]);
        }

        return new LogicAppConfig
        {
            HostingMode = hostingMode,
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
        IConfigurationSection entry = configuration.GetSection(CosmosDbSelector).GetSection(identifier);

        return new CosmosContainerDbConfig
        {
            ConnectionString = entry.GetSection(nameof(CosmosContainerDbConfig.ConnectionString)).Value ?? throw new ConfigurationValidationException(nameof(CosmosContainerDbConfig.ConnectionString)),
            DatabaseName = entry.GetSection(nameof(CosmosContainerDbConfig.DatabaseName)).Value ?? throw new ConfigurationValidationException(nameof(CosmosContainerDbConfig.DatabaseName)),
            ContainerName = entry.GetSection(nameof(CosmosContainerDbConfig.ContainerName)).Value ?? throw new ConfigurationValidationException(nameof(CosmosContainerDbConfig.ContainerName)),

            // Read, not dropped: the record documents that creation "must be opted into explicitly" -
            // an opt-in that a file cannot express is no opt-in, and a configured throughput that never
            // reaches container creation is a silently different container.
            AllowSchemaCreation = ReadOptionalFlag(entry, CosmosDbSelector, identifier, nameof(CosmosContainerDbConfig.AllowSchemaCreation)),
            ProvisionedThroughput = ReadOptionalCount(entry, CosmosDbSelector, identifier, nameof(CosmosContainerDbConfig.ProvisionedThroughput)),
        };
    }

    /// <inheritdoc />
    public ServiceBusConfig LoadServiceBusConfig(IConfiguration configuration, string identifier)
    {
        IConfigurationSection entry = configuration.GetSection(ServiceBusSelector).GetSection(identifier);

        return new ServiceBusConfig
        {
            ConnectionString = entry.GetSection(nameof(ServiceBusConfig.ConnectionString)).Value ?? throw new ConfigurationValidationException(nameof(ServiceBusConfig.ConnectionString)),
            QueueName = entry.GetSection(nameof(ServiceBusConfig.QueueName)).Value,
            TopicName = entry.GetSection(nameof(ServiceBusConfig.TopicName)).Value,
            SubscriptionName = entry.GetSection(nameof(ServiceBusConfig.SubscriptionName)).Value,

            // A named refusal instead of bool.Parse's raw FormatException - the values-path reader
            // already refuses this way, and two readers for one section must agree.
            RequiredSession = ReadOptionalFlag(entry, ServiceBusSelector, identifier, nameof(ServiceBusConfig.RequiredSession)),
        };
    }

    /// <inheritdoc />
    public FunctionAppConfig LoadFunctionAppConfig(IConfiguration configuration, string identifier)
    {
        IConfigurationSection entry = configuration.GetSection(FunctionAppBaseSelector).GetSection(identifier);

        FunctionAppConfig config = new()
        {
            BaseUrl = entry.GetSection(nameof(FunctionAppConfig.BaseUrl)).Value ?? throw new ConfigurationValidationException(nameof(FunctionAppConfig.BaseUrl)),
            Code = entry.GetSection(nameof(FunctionAppConfig.Code)).Value ?? throw new ConfigurationValidationException(nameof(FunctionAppConfig.Code)),
            AdminCode = entry.GetSection(nameof(FunctionAppConfig.AdminCode)).Value,
        };

        // Read, not dropped - and empty is a value: a host running with "routePrefix": "" needs the
        // prefix declared away, and the record's default ("api") must only apply when the file says
        // nothing at all.
        string? routePrefix = entry.GetSection(nameof(FunctionAppConfig.RoutePrefix)).Value;
        return routePrefix is null ? config : config with { RoutePrefix = routePrefix };
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
        };
    }

    private static bool ReadOptionalFlag(IConfigurationSection entry, string selector, string identifier, string property)
    {
        string? value = entry.GetSection(property).Value;
        if (value is null or "")
            return false;

        return bool.TryParse(value, out bool parsed)
            ? parsed
            : throw Unreadable(selector, identifier, property, value, "'true' or 'false'");
    }

    private static int? ReadOptionalCount(IConfigurationSection entry, string selector, string identifier, string property)
    {
        string? value = entry.GetSection(property).Value;
        if (value is null or "")
            return null;

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : throw Unreadable(selector, identifier, property, value, "a whole number");
    }

    private static FrameworkConfigurationException Unreadable(string selector, string identifier, string property, string value, string expected)
        => new($"'{selector}:{identifier}:{property}' holds '{value}', which cannot be read as {expected}.", ["Correct the configuration entry."]);
}