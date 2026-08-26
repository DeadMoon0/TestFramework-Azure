using System;
using System.Collections.Generic;
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

        return values;
    }
}
