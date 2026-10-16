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
/// The storage account section, as the run's resource graph sees it.
/// </summary>
/// <remarks>
/// <para>
/// A configured storage account is a resource somebody declared, and this says which values of the graph's an entry
/// actually holds. What it buys is that a reader asks the run where something is and never learns whether a
/// person wrote it down or something published it while starting.
/// </para>
/// <para>
/// Reading delegates to the package's own provider rather than binding the section again here: two readers for
/// one section is two sets of validation rules, and the second one written is the one that quietly accepts
/// what the first refuses.
/// </para>
/// One account, up to three containers - a blob, a queue and a table container are all things an entry may
/// name and none of them is required, which is what the optional values on the kind say.
/// </remarks>
/// <param name="provider">Reads an entry, including whatever validation the caller configured.</param>
internal sealed class StorageAccountShape(IConfigProvider provider) : ConfigShape<StorageAccountConfig>
{
    /// <inheritdoc />
    public override string Section => DefaultConfigProvider.StorageAccountSelector;

    /// <inheritdoc />
    public override ResourceKind Kind => AzureResourceKinds.StorageKind;

    /// <inheritdoc />
    public override IReadOnlyList<string> Identifiers(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadAllStorageAccountIdentifier(configuration);
    }

    /// <inheritdoc />
    public override StorageAccountConfig Read(IConfiguration configuration, string identifier)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadStorageAccountConfig(configuration, identifier);
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<ValueKey, string> Values(StorageAccountConfig config, string identifier)
    {
        ArgumentNullException.ThrowIfNull(config);

        Dictionary<ValueKey, string> values = [];

        if (config.ConnectionString is { Length: > 0 } connectionString)
        {
            values[new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host)] = connectionString;
            values[new ValueKey(ValueNames.ConnectionString, ResourceVantage.Network)] = connectionString;
        }

        if (config.QueueContainerName is { Length: > 0 } queueContainer)
        {
            values[new ValueKey(AzureResourceKinds.QueueNameValue)] = queueContainer;
        }

        if (config.BlobContainerName is { Length: > 0 } blobContainer)
        {
            values[new ValueKey(AzureResourceKinds.BlobContainerNameValue)] = blobContainer;
        }

        if (config.TableContainerName is { Length: > 0 } tableContainer)
        {
            values[new ValueKey(AzureResourceKinds.TableContainerNameValue)] = tableContainer;
        }

        return values;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The connection string carries the account and its key at once, which is why the kind declares it
    /// secret: it has to travel for anything to connect, and a listing must never print it.
    /// </remarks>
    public override StorageAccountConfig Read(IReadOnlyDictionary<string, string> values, string identifier)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return new StorageAccountConfig
        {
            ConnectionString = Text(values, ValueNames.ConnectionString) ?? throw Missing(ValueNames.ConnectionString, identifier, values),
            QueueContainerName = Text(values, AzureResourceKinds.QueueNameValue),
            BlobContainerName = Text(values, AzureResourceKinds.BlobContainerNameValue),
            TableContainerName = Text(values, AzureResourceKinds.TableContainerNameValue),
        };
    }

}
