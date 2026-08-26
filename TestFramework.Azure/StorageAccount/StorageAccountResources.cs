using System;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.StorageAccount;

/// <summary>
/// Where a storage account actually is, whoever supplied the answer.
/// </summary>
/// <remarks>
/// <para>
/// A configured account has coordinates somebody wrote down. A containerised one gets them when its container
/// starts, on a port the operating system chose. Both arrive as resource values now, so this asks the run once
/// and callers stop having to know which happened - the branch that used to be spelled "ask the environment,
/// else read the file", and the reason a starting container used to write back into this package's
/// configuration store.
/// </para>
/// <para>
/// What comes back is the ordinary configuration record, because that is what every caller already hands to a
/// client factory. The graph supplies what it knows; the declaration supplies the rest. A produced value wins,
/// which is the whole point: the container that just started knows its port and the file does not.
/// </para>
/// <para>
/// Public because the answer is not this package's private business. An environment that provisions these
/// resources has to read the same coordinates to configure the things it starts - a Function App's settings
/// name the storage account and the bus it binds to - and the alternative it reached for before was the
/// configuration store, which is how a started container's address ended up being written back into it.
/// </para>
/// <para>
/// This and <c>StorageAccountShape.Values</c> are the two directions of one mapping and they have to agree.
/// A round-trip test is what makes that true rather than hoped for - it puts a record through the shape into a
/// graph and reads it back through here.
/// </para>
/// </remarks>
public static class StorageAccountResources
{
    /// <summary>
    /// The account's configuration, with whatever the run knows about where it is filled in.
    /// </summary>
    /// <remarks>
    /// The entry still has to exist. An account that only a container ever mentions has nothing to fall back
    /// to, and this package's configuration record cannot yet express "a name, coordinates to follow" - see
    /// entry 21 of the debt ledger, which is about exactly that.
    /// </remarks>
    /// <param name="context">The run.</param>
    /// <param name="identifier">Which account.</param>
    /// <returns>The configuration a client can be built from.</returns>
    public static StorageAccountConfig Resolve(RunContext context, string identifier)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        StorageAccountConfig declared = context.Services
            .GetRequiredService<ConfigStore<StorageAccountConfig>>()
            .GetConfig(identifier);

        return declared with
        {
            ConnectionString = Value(context, identifier, ValueNames.ConnectionString) ?? declared.ConnectionString,
            BlobContainerName = Value(context, identifier, AzureEnvironmentResourceKinds.BlobContainerNameValue) ?? declared.BlobContainerName,
            QueueContainerName = Value(context, identifier, AzureEnvironmentResourceKinds.QueueNameValue) ?? declared.QueueContainerName,
            TableContainerName = Value(context, identifier, AzureEnvironmentResourceKinds.TableContainerNameValue) ?? declared.TableContainerName,
        };
    }

    /// <summary>
    /// One value from the run, from the viewpoint of the process asking.
    /// </summary>
    /// <remarks>
    /// The host viewpoint, because this runs in the test process. Code inside the network asks for the same
    /// value name from its own viewpoint and gets the address that works there - which is the reason a
    /// coordinate is published twice and a name once.
    /// </remarks>
    private static string? Value(RunContext context, string identifier, string valueName)
        => context.Values.TryGet(
            ValueRef.For(AzureEnvironmentResourceKinds.Storage, identifier, valueName),
            ResourceVantage.Host,
            out string? value)
            ? value
            : null;
}
