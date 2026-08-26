using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Config.Configuration;
using TestFramework.Core.Environment.Graph;

namespace TestFramework.Azure.Configuration;

/// <summary>
/// The Function App section, as the run's resource graph sees it.
/// </summary>
/// <remarks>
/// <para>
/// A configured Function App is a resource somebody declared, and this says which values of the graph's an entry
/// actually holds. What it buys is that a reader asks the run where something is and never learns whether a
/// person wrote it down or something published it while starting.
/// </para>
/// <para>
/// Reading delegates to the package's own provider rather than binding the section again here: two readers for
/// one section is two sets of validation rules, and the second one written is the one that quietly accepts
/// what the first refuses.
/// </para>
/// The route prefix travels with the address because it is part of reaching a function - a caller composing a
/// URL needs both. The function key does not: it says who you are rather than where the app is, and the steps
/// that need it read it from the typed configuration.
/// </remarks>
/// <param name="provider">Reads an entry, including whatever validation the caller configured.</param>
internal sealed class FunctionAppShape(IConfigProvider provider) : ConfigShape<FunctionAppConfig>
{
    /// <inheritdoc />
    public override string Section => DefaultConfigProvider.FunctionAppBaseSelector;

    /// <inheritdoc />
    public override ResourceKind Kind => AzureEnvironmentResourceKinds.FunctionAppKind;

    /// <inheritdoc />
    public override IReadOnlyList<string> Identifiers(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadAllFunctionAppIdentifier(configuration);
    }

    /// <inheritdoc />
    public override FunctionAppConfig Read(IConfiguration configuration, string identifier)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadFunctionAppConfig(configuration, identifier);
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<ValueKey, string> Values(FunctionAppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Dictionary<ValueKey, string> values = [];

        if (config.BaseUrl is { Length: > 0 } baseUrl)
        {
            values[new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host)] = baseUrl;
            values[new ValueKey(ValueNames.BaseUrl, ResourceVantage.Network)] = baseUrl;
        }

        if (config.RoutePrefix is { Length: > 0 } routePrefix)
        {
            values[new ValueKey(AzureEnvironmentResourceKinds.RoutePrefixValue)] = routePrefix;
        }

        return values;
    }
}
