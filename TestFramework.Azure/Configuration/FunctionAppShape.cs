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
/// <para>
/// The whole entry travels, keys included. That reverses an earlier line here - a credential used to stay out
/// of the graph and live in a typed configuration store instead - which held only while there was a store to
/// live in. A key is declared secret on the kind now, so it reaches whatever authenticates and never reaches
/// a listing.
/// </para>
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

        // Always, empty included. An empty route prefix is a stated choice - it means "call the site root" -
        // and skipping it because it is empty would make the reader fall back to the default "api", which is
        // the opposite of what was asked for. "Absent" and "empty" are different answers for a non-nullable
        // property with a default, and only the one that was never written down may take the default.
        values[new ValueKey(AzureEnvironmentResourceKinds.RoutePrefixValue)] = config.RoutePrefix ?? string.Empty;

        if (config.Code is { Length: > 0 } code)
        {
            values[new ValueKey(AzureEnvironmentResourceKinds.CodeValue)] = code;
        }

        if (config.AdminCode is { Length: > 0 } adminCode)
        {
            values[new ValueKey(AzureEnvironmentResourceKinds.AdminCodeValue)] = adminCode;
        }

        return values;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The keys travel and are declared secret on the kind. A function key is a credential that is not also
    /// an address - unlike a connection string - so redacting it in a listing costs a reader nothing, and it
    /// is the reason this entry could not be rebuilt from coordinates alone before secrets could travel.
    /// </remarks>
    public override FunctionAppConfig Read(IReadOnlyDictionary<string, string> values, string identifier)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        FunctionAppConfig config = new FunctionAppConfig
        {
            BaseUrl = Text(values, ValueNames.BaseUrl) ?? throw Missing(ValueNames.BaseUrl, identifier, values),
            Code = Text(values, AzureEnvironmentResourceKinds.CodeValue),
            AdminCode = Text(values, AzureEnvironmentResourceKinds.AdminCodeValue),
        };

        // Presence, not emptiness - see the note in Values.
        if (values.TryGetValue(AzureEnvironmentResourceKinds.RoutePrefixValue, out string? routePrefix))
        {
            config = config with { RoutePrefix = routePrefix };
        }

        return config;
    }

}
