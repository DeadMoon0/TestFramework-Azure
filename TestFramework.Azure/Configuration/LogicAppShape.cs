using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.LogicApp;
using TestFramework.Config.Configuration;
using TestFramework.Core.Environment.Graph;

namespace TestFramework.Azure.Configuration;

/// <summary>
/// The Logic App section, as the run's resource graph sees it.
/// </summary>
/// <remarks>
/// <para>
/// A configured Logic App is a resource somebody declared, and this says which values of the graph's an entry
/// actually holds. What it buys is that a reader asks the run where something is and never learns whether a
/// person wrote it down or something published it while starting.
/// </para>
/// <para>
/// Reading delegates to the package's own provider rather than binding the section again here: two readers for
/// one section is two sets of validation rules, and the second one written is the one that quietly accepts
/// what the first refuses.
/// </para>
/// One address out of two possible homes. Which hosting mode an entry declared decides where its address was
/// written - the Standard host URL or the Consumption invoke URL - and a reader wants neither of those
/// distinctions, only the address, so the shape resolves it here rather than making everybody ask twice.
/// </remarks>
/// <param name="provider">Reads an entry, including whatever validation the caller configured.</param>
internal sealed class LogicAppShape(IConfigProvider provider) : ConfigShape<LogicAppConfig>
{
    /// <inheritdoc />
    public override string Section => DefaultConfigProvider.LogicAppSelector;

    /// <inheritdoc />
    public override ResourceKind Kind => AzureEnvironmentResourceKinds.LogicAppKind;

    /// <inheritdoc />
    public override IReadOnlyList<string> Identifiers(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadAllLogicAppIdentifier(configuration);
    }

    /// <inheritdoc />
    public override LogicAppConfig Read(IConfiguration configuration, string identifier)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadLogicAppConfig(configuration, identifier);
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<ValueKey, string> Values(LogicAppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Dictionary<ValueKey, string> values = [];

        string? baseUrl = config.HostingMode == LogicAppHostingMode.Consumption
            ? config.Consumption.InvokeUrl
            : config.Standard.BaseUrl;

        if (baseUrl is { Length: > 0 } resolved)
        {
            values[new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host)] = resolved;
            values[new ValueKey(ValueNames.BaseUrl, ResourceVantage.Network)] = resolved;
        }

        return values;
    }
}
