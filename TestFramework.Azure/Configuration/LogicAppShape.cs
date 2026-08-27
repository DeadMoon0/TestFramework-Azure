using System;
using System.Collections.Generic;
using TestFramework.Core.Exceptions;
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

        // Flattened, because a value is a name and a string: the nested records are how an author writes two
        // mutually exclusive hosting modes down, not two shapes a reader has to handle. Which mode an entry
        // declared decides which of them held anything, and that answer is one value.
        values[new ValueKey(nameof(LogicAppConfig.HostingMode))] = config.HostingMode.ToString();

        Add(values, nameof(LogicAppConfig.WorkflowName), config.WorkflowName);
        Add(values, StandardCode, config.Standard.Code);
        Add(values, StandardAdminCode, config.Standard.AdminCode);
        Add(values, ConsumptionResourceId, config.Consumption.WorkflowResourceId);

        return values;
    }

    /// <summary>The value name the Standard host's function key travels under.</summary>
    private const string StandardCode = "Standard:Code";

    /// <summary>The value name the Standard host's admin key travels under.</summary>
    private const string StandardAdminCode = "Standard:AdminCode";

    /// <summary>The value name the Consumption workflow's resource id travels under.</summary>
    private const string ConsumptionResourceId = "Consumption:WorkflowResourceId";

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The one entry in the family whose record nests, and rebuilding it is where the nesting turns out to be
    /// an authoring convenience rather than structure a reader needs. The address is already resolved to one
    /// value by <see cref="Values"/> - whichever of the two homes the declared hosting mode chose - so this
    /// puts it back into the half that mode reads from.
    /// </para>
    /// <para>
    /// The keys keep their colon-separated names, which is what the configuration section calls them, so an
    /// entry and its values read the same way round.
    /// </para>
    /// </remarks>
    public override LogicAppConfig Read(IReadOnlyDictionary<string, string> values, string identifier)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        LogicAppHostingMode hostingMode = LogicAppHostingMode.Standard;

        if (values.TryGetValue(nameof(LogicAppConfig.HostingMode), out string? declaredMode))
        {
            hostingMode = Enum.TryParse(declaredMode, ignoreCase: true, out LogicAppHostingMode parsed)
                ? parsed
                : throw new FrameworkConfigurationException(
                    $"The run holds '{declaredMode}' as {nameof(LogicAppConfig.HostingMode)} for Logic App '{identifier}', which is not a hosting mode.",
                    [$"Use one of: {string.Join(", ", Enum.GetNames<LogicAppHostingMode>())}."]);
        }

        string? address = Text(values, ValueNames.BaseUrl);

        return new LogicAppConfig
        {
            HostingMode = hostingMode,
            WorkflowName = Text(values, nameof(LogicAppConfig.WorkflowName)),
            Standard = new LogicAppStandardConfig
            {
                BaseUrl = hostingMode == LogicAppHostingMode.Consumption ? null : address,
                Code = Text(values, StandardCode),
                AdminCode = Text(values, StandardAdminCode),
            },
            Consumption = new LogicAppConsumptionConfig
            {
                InvokeUrl = hostingMode == LogicAppHostingMode.Consumption ? address : null,
                WorkflowResourceId = Text(values, ConsumptionResourceId),
            },
        };
    }

    private static void Add(Dictionary<ValueKey, string> values, string valueName, string? value)
    {
        if (value is { Length: > 0 })
        {
            values[new ValueKey(valueName)] = value;
        }
    }

    private static string? Text(IReadOnlyDictionary<string, string> values, string valueName)
        => values.TryGetValue(valueName, out string? value) && value is { Length: > 0 } ? value : null;
}
