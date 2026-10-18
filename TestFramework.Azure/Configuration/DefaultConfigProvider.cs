using System;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Linq;
using TestFramework.Azure.Configuration.SpecificConfigs;
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
    public const string FunctionAppSelector = "FunctionApp";

    /// <summary>
    /// Configuration section name for <see cref="StorageAccountConfig"/> records.
    /// </summary>
    public const string StorageAccountSelector = "StorageAccount";

    /// <summary>
    /// Configuration section name for <see cref="CosmosDbConfig"/> records.
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
    public string[] LoadAllFunctionAppIdentifier(IConfiguration configuration) => [.. configuration.GetSection(FunctionAppSelector).GetChildren().Select(x => x.Key)];

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
                    $"'{LogicAppSelector}:{identifier}:{nameof(LogicAppConfig.HostingMode)}' holds '{declaredMode}', which is not a hosting mode.")
                {
                    RecoverySteps = [$"Use one of: {string.Join(", ", Enum.GetNames<LogicAppHostingMode>())}."],
                };
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
    public CosmosDbConfig LoadCosmosDbConfig(IConfiguration configuration, string identifier)
    {
        IConfigurationSection entry = configuration.GetSection(CosmosDbSelector).GetSection(identifier);

        return new CosmosDbConfig
        {
            ConnectionString = Required(entry, CosmosDbSelector, identifier, nameof(CosmosDbConfig.ConnectionString)),
            DatabaseName = Required(entry, CosmosDbSelector, identifier, nameof(CosmosDbConfig.DatabaseName)),
            ContainerName = Required(entry, CosmosDbSelector, identifier, nameof(CosmosDbConfig.ContainerName)),

            // Read, not dropped: the record documents that creation "must be opted into explicitly" -
            // an opt-in that a file cannot express is no opt-in, and a configured throughput that never
            // reaches container creation is a silently different container.
            AllowSchemaCreation = ReadOptionalFlag(entry, CosmosDbSelector, identifier, nameof(CosmosDbConfig.AllowSchemaCreation)),
            ProvisionedThroughput = ReadOptionalCount(entry, CosmosDbSelector, identifier, nameof(CosmosDbConfig.ProvisionedThroughput)),
        };
    }

    /// <inheritdoc />
    public ServiceBusConfig LoadServiceBusConfig(IConfiguration configuration, string identifier)
    {
        IConfigurationSection entry = configuration.GetSection(ServiceBusSelector).GetSection(identifier);
        RefuseRenamedSetting(entry, ServiceBusSelector, identifier, "RequiredSession", nameof(ServiceBusConfig.RequiresSession));

        return new ServiceBusConfig
        {
            ConnectionString = Required(entry, ServiceBusSelector, identifier, nameof(ServiceBusConfig.ConnectionString)),
            QueueName = entry.GetSection(nameof(ServiceBusConfig.QueueName)).Value,
            TopicName = entry.GetSection(nameof(ServiceBusConfig.TopicName)).Value,
            SubscriptionName = entry.GetSection(nameof(ServiceBusConfig.SubscriptionName)).Value,

            // A named refusal instead of bool.Parse's raw FormatException - the values-path reader
            // already refuses this way, and two readers for one section must agree.
            RequiresSession = ReadOptionalFlag(entry, ServiceBusSelector, identifier, nameof(ServiceBusConfig.RequiresSession)),
        }.EnsureNamesOneEntity(identifier);
    }

    /// <inheritdoc />
    public FunctionAppConfig LoadFunctionAppConfig(IConfiguration configuration, string identifier)
    {
        IConfigurationSection entry = configuration.GetSection(FunctionAppSelector).GetSection(identifier);

        FunctionAppConfig config = new()
        {
            BaseUrl = Required(entry, FunctionAppSelector, identifier, nameof(FunctionAppConfig.BaseUrl)),
            Code = entry.GetSection(nameof(FunctionAppConfig.Code)).Value,
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
        IConfigurationSection entry = configuration.GetSection(StorageAccountSelector).GetSection(identifier);
        RefuseRenamedSetting(entry, StorageAccountSelector, identifier, "QueueContainerName", nameof(StorageAccountConfig.QueueName));
        RefuseRenamedSetting(entry, StorageAccountSelector, identifier, "TableContainerName", nameof(StorageAccountConfig.TableName));

        return new StorageAccountConfig
        {
            ConnectionString = Required(entry, StorageAccountSelector, identifier, nameof(StorageAccountConfig.ConnectionString)),
            BlobContainerName = entry.GetSection(nameof(StorageAccountConfig.BlobContainerName)).Value,
            QueueName = entry.GetSection(nameof(StorageAccountConfig.QueueName)).Value,
            TableName = entry.GetSection(nameof(StorageAccountConfig.TableName)).Value,
        };
    }

    /// <inheritdoc />
    public SqlDatabaseConfig LoadSqlDatabaseConfig(IConfiguration configuration, string identifier)
    {
        return new SqlDatabaseConfig
        {
            ConnectionString = Required(configuration.GetSection(SqlDatabaseSelector).GetSection(identifier), SqlDatabaseSelector, identifier, nameof(SqlDatabaseConfig.ConnectionString)),
            DatabaseName = Required(configuration.GetSection(SqlDatabaseSelector).GetSection(identifier), SqlDatabaseSelector, identifier, nameof(SqlDatabaseConfig.DatabaseName)),
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

    /// <summary>
    /// Reads a setting the entry cannot do without, refusing it by its full path when it is absent or empty.
    /// </summary>
    /// <remarks>
    /// The path is the thing to open: with six sections and several entries each, "ConnectionString cannot be Empty
    /// or Null" did not say which file entry to fix.
    /// </remarks>
    private static string Required(IConfigurationSection entry, string selector, string identifier, string property)
    {
        string? value = entry.GetSection(property).Value;
        if (value is { Length: > 0 })
            return value;

        string path = $"{selector}:{identifier}:{property}";
        throw new FrameworkConfigurationException(
            entry.Exists()
                ? $"'{path}' is missing, and the '{selector}:{identifier}' entry needs it."
                : $"'{path}' is missing: there is no '{selector}:{identifier}' entry in the configuration.")
        {
            RecoverySteps = [$"Set it in the configuration, for example \"{selector}\": {{ \"{identifier}\": {{ \"{property}\": \"...\" }} }} in the test settings file."],
        };
    }

    /// <summary>
    /// Refuses an entry that still spells a setting by the name it had before, naming the one it has now.
    /// </summary>
    /// <remarks>
    /// There is no bridge from the old name. Read as it is, an entry under the old name would load as if the setting
    /// were absent, and the run would fail later on a missing queue or table - or, for a session flag, build the wrong
    /// receiver without failing at all. Refusing it here names the one line to change.
    /// </remarks>
    private static void RefuseRenamedSetting(IConfigurationSection entry, string selector, string identifier, string oldName, string newName)
    {
        if (!entry.GetSection(oldName).Exists())
            return;

        throw new FrameworkConfigurationException(
            $"'{selector}:{identifier}:{oldName}' is no longer read: the setting is now called '{newName}'.")
        {
            RecoverySteps = [$"Rename '{oldName}' to '{newName}' in the '{selector}:{identifier}' entry."],
        };
    }

    private static FrameworkConfigurationException Unreadable(string selector, string identifier, string property, string value, string expected)
        => new($"'{selector}:{identifier}:{property}' holds '{value}', which cannot be read as {expected}.")
        {
            RecoverySteps = ["Correct the configuration entry."],
        };
}