using System;
using System.Collections.Generic;
using TestFramework.Azure.Configuration.SpecificConfigs;

namespace TestFramework.Azure.Configuration;

/// <summary>
/// Default <see cref="IConfigExporter"/> implementation that emits the keys expected by <see cref="DefaultConfigProvider"/>.
/// </summary>
public sealed class DefaultConfigExporter : IConfigExporter
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ExportLogicAppConfig(string identifier, LogicAppConfig config) =>
        CreateSection(
            DefaultConfigProvider.LogicAppSelector,
            identifier,
            (nameof(LogicAppConfig.HostingMode), config.HostingMode.ToString()),
            (nameof(LogicAppConfig.WorkflowName), config.WorkflowName),
            ($"{nameof(LogicAppConfig.Standard)}:{nameof(LogicAppStandardConfig.BaseUrl)}", config.Standard.BaseUrl),
            ($"{nameof(LogicAppConfig.Standard)}:{nameof(LogicAppStandardConfig.Code)}", config.Standard.Code),
            ($"{nameof(LogicAppConfig.Standard)}:{nameof(LogicAppStandardConfig.AdminCode)}", config.Standard.AdminCode),
            ($"{nameof(LogicAppConfig.Consumption)}:{nameof(LogicAppConsumptionConfig.InvokeUrl)}", config.Consumption.InvokeUrl),
            ($"{nameof(LogicAppConfig.Consumption)}:{nameof(LogicAppConsumptionConfig.WorkflowResourceId)}", config.Consumption.WorkflowResourceId));

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ExportFunctionAppConfig(string identifier, FunctionAppConfig config) =>
        CreateSection(
            DefaultConfigProvider.FunctionAppSelector,
            identifier,
            (nameof(FunctionAppConfig.BaseUrl), config.BaseUrl),
            (nameof(FunctionAppConfig.Code), config.Code),
            (nameof(FunctionAppConfig.AdminCode), config.AdminCode),
            (nameof(FunctionAppConfig.RoutePrefix), config.RoutePrefix));

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ExportStorageAccountConfig(string identifier, StorageAccountConfig config) =>
        CreateSection(
            DefaultConfigProvider.StorageAccountSelector,
            identifier,
            (nameof(StorageAccountConfig.ConnectionString), config.ConnectionString),
            (nameof(StorageAccountConfig.QueueName), config.QueueName),
            (nameof(StorageAccountConfig.BlobContainerName), config.BlobContainerName),
            (nameof(StorageAccountConfig.TableName), config.TableName));

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ExportCosmosDbConfig(string identifier, CosmosDbConfig config) =>
        CreateSection(
            DefaultConfigProvider.CosmosDbSelector,
            identifier,
            (nameof(CosmosDbConfig.ConnectionString), config.ConnectionString),
            (nameof(CosmosDbConfig.DatabaseName), config.DatabaseName),
            (nameof(CosmosDbConfig.ContainerName), config.ContainerName),
            (nameof(CosmosDbConfig.AllowSchemaCreation), config.AllowSchemaCreation ? bool.TrueString : null),
            (nameof(CosmosDbConfig.ProvisionedThroughput), config.ProvisionedThroughput?.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ExportServiceBusConfig(string identifier, ServiceBusConfig config) =>
        CreateSection(
            DefaultConfigProvider.ServiceBusSelector,
            identifier,
            (nameof(ServiceBusConfig.ConnectionString), config.ConnectionString),
            (nameof(ServiceBusConfig.QueueName), config.QueueName),
            (nameof(ServiceBusConfig.TopicName), config.TopicName),
            (nameof(ServiceBusConfig.SubscriptionName), config.SubscriptionName),
            (nameof(ServiceBusConfig.RequiresSession), config.RequiresSession.ToString()));

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ExportSqlDatabaseConfig(string identifier, SqlDatabaseConfig config) =>
        CreateSection(
            DefaultConfigProvider.SqlDatabaseSelector,
            identifier,
            (nameof(SqlDatabaseConfig.ConnectionString), config.ConnectionString),
            (nameof(SqlDatabaseConfig.DatabaseName), config.DatabaseName));

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