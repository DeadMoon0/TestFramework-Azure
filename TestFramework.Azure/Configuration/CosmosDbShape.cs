using System;
using System.Collections.Generic;
using TestFramework.Core.Exceptions;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Config.Configuration;
using TestFramework.Core.Environment.Graph;

namespace TestFramework.Azure.Configuration;

/// <summary>
/// The Cosmos container section, as the run's resource graph sees it.
/// </summary>
/// <remarks>
/// <para>
/// A configured Cosmos container is a resource somebody declared, and this says which values of the graph's an entry
/// actually holds. What it buys is that a reader asks the run where something is and never learns whether a
/// person wrote it down or something published it while starting.
/// </para>
/// <para>
/// Reading delegates to the package's own provider rather than binding the section again here: two readers for
/// one section is two sets of validation rules, and the second one written is the one that quietly accepts
/// what the first refuses.
/// </para>
/// A Cosmos entry names an account, a database and a container, and all three are needed to reach an item -
/// so all three are published. The database and container names are single values; only the account's address
/// depends on who is asking.
/// </remarks>
/// <param name="provider">Reads an entry, including whatever validation the caller configured.</param>
internal sealed class CosmosDbShape(IConfigProvider provider) : ConfigShape<CosmosContainerDbConfig>
{
    /// <inheritdoc />
    public override string Section => DefaultConfigProvider.CosmosDbSelector;

    /// <inheritdoc />
    public override ResourceKind Kind => AzureEnvironmentResourceKinds.CosmosKind;

    /// <inheritdoc />
    public override IReadOnlyList<string> Identifiers(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadAllCosmosDbIdentifier(configuration);
    }

    /// <inheritdoc />
    public override CosmosContainerDbConfig Read(IConfiguration configuration, string identifier)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadCosmosDbConfig(configuration, identifier);
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<ValueKey, string> Values(CosmosContainerDbConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Dictionary<ValueKey, string> values = [];

        if (config.ConnectionString is { Length: > 0 } connectionString)
        {
            values[new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host)] = connectionString;
            values[new ValueKey(ValueNames.ConnectionString, ResourceVantage.Network)] = connectionString;
        }

        if (config.DatabaseName is { Length: > 0 } databaseName)
        {
            values[new ValueKey(AzureEnvironmentResourceKinds.DatabaseNameValue)] = databaseName;
        }

        if (config.ContainerName is { Length: > 0 } containerName)
        {
            values[new ValueKey(AzureEnvironmentResourceKinds.ContainerNameValue)] = containerName;
        }

        if (config.ProvisionedThroughput is { } throughput)
        {
            values[new ValueKey(AzureEnvironmentResourceKinds.ProvisionedThroughputValue)] = throughput.ToString(CultureInfo.InvariantCulture);
        }

        values[new ValueKey(AzureEnvironmentResourceKinds.AllowSchemaCreationValue)] = config.AllowSchemaCreation ? "true" : "false";

        return values;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Throughput and the schema-creation tolerance are declarations the <em>environment</em> reads while
    /// provisioning rather than a step reads while running - which is a difference in who asks, not in what
    /// the value is, so they travel the same way as everything else.
    /// </remarks>
    public override CosmosContainerDbConfig Read(IReadOnlyDictionary<string, string> values, string identifier)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        CosmosContainerDbConfig config = new CosmosContainerDbConfig
        {
            ConnectionString = Text(values, ValueNames.ConnectionString) ?? throw Missing(ValueNames.ConnectionString, identifier, values),
            DatabaseName = Text(values, AzureEnvironmentResourceKinds.DatabaseNameValue) ?? throw Missing(AzureEnvironmentResourceKinds.DatabaseNameValue, identifier, values),
            ContainerName = Text(values, AzureEnvironmentResourceKinds.ContainerNameValue) ?? throw Missing(AzureEnvironmentResourceKinds.ContainerNameValue, identifier, values),
            AllowSchemaCreation = Flag(values, AzureEnvironmentResourceKinds.AllowSchemaCreationValue, identifier, fallback: false),
        };

        if (values.TryGetValue(AzureEnvironmentResourceKinds.ProvisionedThroughputValue, out string? throughput))
        {
            config = config with
            {
                ProvisionedThroughput = int.TryParse(throughput, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                    ? parsed
                    : throw Unreadable(throughput, identifier, AzureEnvironmentResourceKinds.ProvisionedThroughputValue, "a whole number of request units"),
            };
        }

        return config;
    }

    private static string? Text(IReadOnlyDictionary<string, string> values, string valueName)
        => values.TryGetValue(valueName, out string? value) && value is { Length: > 0 } ? value : null;

    private static bool Flag(IReadOnlyDictionary<string, string> values, string valueName, string identifier, bool fallback)
    {
        if (!values.TryGetValue(valueName, out string? value))
        {
            return fallback;
        }

        return bool.TryParse(value, out bool parsed)
            ? parsed
            : throw Unreadable(value, identifier, valueName, "'true' or 'false'");
    }

    private static FrameworkConfigurationException Unreadable(string value, string identifier, string valueName, string expected)
        => new FrameworkConfigurationException(
            $"The run holds '{value}' as {valueName} for '{identifier}', which cannot be read as {expected}.",
            [
                $"Correct the configuration entry, or the environment component that publishes it.",
            ]);

    private static FrameworkConfigurationException Missing(string valueName, string identifier, IReadOnlyDictionary<string, string> values)
        => new FrameworkConfigurationException(
            $"The run has no {valueName} for '{identifier}'.",
            [
                "Add it to the configuration entry, or include the definition that provisions the resource.",
            ],
            [.. values.Keys]);
}
