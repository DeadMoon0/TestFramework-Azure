using System;
using System.Collections.Generic;
using TestFramework.Core.Exceptions;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Config.Configuration;
using TestFramework.Core.Environment.Graph;
using static TestFramework.Azure.Configuration.ShapeValues;

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
internal sealed class CosmosDbShape(IConfigProvider provider) : ConfigShape<CosmosDbConfig>
{
    /// <inheritdoc />
    public override string Section => DefaultConfigProvider.CosmosDbSelector;

    /// <inheritdoc />
    public override ResourceKind Kind => AzureResourceKinds.CosmosDbKind;

    /// <inheritdoc />
    public override IReadOnlyList<string> Identifiers(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadAllCosmosDbIdentifier(configuration);
    }

    /// <inheritdoc />
    public override CosmosDbConfig Read(IConfiguration configuration, string identifier)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadCosmosDbConfig(configuration, identifier);
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<ValueKey, string> Values(CosmosDbConfig config, string identifier)
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
            values[new ValueKey(AzureResourceKinds.DatabaseNameValue)] = databaseName;
        }

        if (config.ContainerName is { Length: > 0 } containerName)
        {
            values[new ValueKey(AzureResourceKinds.ContainerNameValue)] = containerName;
        }

        if (config.ProvisionedThroughput is { } throughput)
        {
            values[new ValueKey(AzureResourceKinds.ProvisionedThroughputValue)] = throughput.ToString(CultureInfo.InvariantCulture);
        }

        values[new ValueKey(AzureResourceKinds.AllowSchemaCreationValue)] = config.AllowSchemaCreation ? "true" : "false";

        return values;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Throughput and the schema-creation tolerance are declarations the <em>environment</em> reads while
    /// provisioning rather than a step reads while running - which is a difference in who asks, not in what
    /// the value is, so they travel the same way as everything else.
    /// </remarks>
    public override CosmosDbConfig Read(IReadOnlyDictionary<string, string> values, string identifier)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        CosmosDbConfig config = new CosmosDbConfig
        {
            ConnectionString = Text(values, ValueNames.ConnectionString) ?? throw Missing(ValueNames.ConnectionString, identifier, values),
            DatabaseName = Text(values, AzureResourceKinds.DatabaseNameValue) ?? throw Missing(AzureResourceKinds.DatabaseNameValue, identifier, values),
            ContainerName = Text(values, AzureResourceKinds.ContainerNameValue) ?? throw Missing(AzureResourceKinds.ContainerNameValue, identifier, values),
            AllowSchemaCreation = Flag(values, AzureResourceKinds.AllowSchemaCreationValue, identifier, fallback: false),
        };

        if (values.TryGetValue(AzureResourceKinds.ProvisionedThroughputValue, out string? throughput))
        {
            config = config with
            {
                ProvisionedThroughput = int.TryParse(throughput, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                    ? parsed
                    : throw Unreadable(throughput, identifier, AzureResourceKinds.ProvisionedThroughputValue, "a whole number of request units"),
            };
        }

        return config;
    }

}
