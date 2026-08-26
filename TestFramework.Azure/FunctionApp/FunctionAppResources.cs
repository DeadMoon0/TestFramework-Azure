using System;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.FunctionApp;

/// <summary>
/// Where a Function App actually is, whoever supplied the answer.
/// </summary>
/// <remarks>
/// <para>
/// A configured resource has coordinates somebody wrote down. A containerised one gets them when its container
/// starts, on a port the operating system chose. Both arrive as resource values, so this asks the run once and
/// callers stop having to know which happened - the branch that used to be spelled "ask the environment, else
/// read the file", and the reason a starting container used to write back into this package's configuration
/// store.
/// </para>
/// <para>
/// What comes back is the ordinary configuration record, because that is what callers already hand to a client
/// factory. A produced value wins over a declared one: the container that just started knows its port and the
/// file does not. Nothing produced means the declaration answers, which is what a deployed resource needs.
/// </para>
/// <para>
/// Public because the answer is not this package's private business. An environment that provisions these
/// resources has to read the same coordinates to configure the things it starts - a Function App's settings
/// name the storage account and the bus it binds to - and the alternative it reached for before was the
/// configuration store, which is how a started container's address ended up being written back into it.
/// </para>
/// <para>
/// This and <c>FunctionAppShape.Values</c> are the two directions of one mapping and they have to agree. A round-trip
/// test is what makes that true rather than hoped for.
/// </para>
/// </remarks>
public static class FunctionAppResources
{
    /// <summary>
    /// The resource's configuration, with whatever the run knows about where it is filled in.
    /// </summary>
    /// <remarks>
    /// The entry still has to exist - see entry 21 of the debt ledger, which is about a record that cannot yet
    /// say "a name, coordinates to follow". An environment that provisions a resource nobody configured
    /// supplies a default entry for it, which is why this holds in practice.
    /// </remarks>
    /// <param name="context">The run.</param>
    /// <param name="identifier">Which resource.</param>
    /// <returns>The configuration a client can be built from.</returns>
    public static FunctionAppConfig Resolve(RunContext context, string identifier)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        FunctionAppConfig declared = context.Services
            .GetRequiredService<ConfigStore<FunctionAppConfig>>()
            .GetConfig(identifier);

        return declared with
        {
            BaseUrl = Value(context, identifier, ValueNames.BaseUrl) ?? declared.BaseUrl,
            RoutePrefix = Value(context, identifier, AzureEnvironmentResourceKinds.RoutePrefixValue) ?? declared.RoutePrefix,
        };
    }

    /// <summary>
    /// One value from the run, from the viewpoint of the process asking.
    /// </summary>
    /// <remarks>
    /// The host viewpoint, because this runs in the test process. Code inside the network asks for the same
    /// value name from its own viewpoint and gets the address that works there.
    /// </remarks>
    private static string? Value(RunContext context, string identifier, string valueName)
        => context.Values.TryGet(
            ValueRef.For(AzureEnvironmentResourceKinds.FunctionApp, identifier, valueName),
            ResourceVantage.Host,
            out string? value)
            ? value
            : null;
}
