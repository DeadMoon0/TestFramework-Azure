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
/// The Service Bus namespace section, as the run's resource graph sees it.
/// </summary>
/// <remarks>
/// <para>
/// A configured Service Bus namespace is a resource somebody declared, and this says which values of the graph's an entry
/// actually holds. What it buys is that a reader asks the run where something is and never learns whether a
/// person wrote it down or something published it while starting.
/// </para>
/// <para>
/// Reading delegates to the package's own provider rather than binding the section again here: two readers for
/// one section is two sets of validation rules, and the second one written is the one that quietly accepts
/// what the first refuses.
/// </para>
/// The entities an entry names travel with it, because "which queue" is as much a part of reaching a message
/// as "which namespace". They are single values rather than per viewpoint: a topic is called the same thing
/// from inside the network as from outside it.
/// </remarks>
/// <param name="provider">Reads an entry, including whatever validation the caller configured.</param>
internal sealed class ServiceBusShape(IConfigProvider provider) : ConfigShape<ServiceBusConfig>
{
    /// <inheritdoc />
    public override string Section => DefaultConfigProvider.ServiceBusSelector;

    /// <inheritdoc />
    public override ResourceKind Kind => AzureEnvironmentResourceKinds.ServiceBusKind;

    /// <inheritdoc />
    public override IReadOnlyList<string> Identifiers(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadAllServiceBusIdentifier(configuration);
    }

    /// <inheritdoc />
    public override ServiceBusConfig Read(IConfiguration configuration, string identifier)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadServiceBusConfig(configuration, identifier);
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<ValueKey, string> Values(ServiceBusConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Dictionary<ValueKey, string> values = [];

        if (config.ConnectionString is { Length: > 0 } connectionString)
        {
            values[new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host)] = connectionString;
            values[new ValueKey(ValueNames.ConnectionString, ResourceVantage.Network)] = connectionString;
        }

        if (config.QueueName is { Length: > 0 } queueName)
        {
            values[new ValueKey(AzureEnvironmentResourceKinds.QueueNameValue)] = queueName;
        }

        if (config.TopicName is { Length: > 0 } topicName)
        {
            values[new ValueKey(AzureEnvironmentResourceKinds.TopicNameValue)] = topicName;
        }

        if (config.SubscriptionName is { Length: > 0 } subscriptionName)
        {
            values[new ValueKey(AzureEnvironmentResourceKinds.SubscriptionNameValue)] = subscriptionName;
        }

        values[new ValueKey(AzureEnvironmentResourceKinds.RequiredSessionValue)] = config.RequiredSession ? "true" : "false";

        return values;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The session requirement travels because it shapes how a receiver is built rather than only how a
    /// message is read - a reader that guessed it would build the wrong processor.
    /// </remarks>
    public override ServiceBusConfig Read(IReadOnlyDictionary<string, string> values, string identifier)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return new ServiceBusConfig
        {
            ConnectionString = Text(values, ValueNames.ConnectionString) ?? throw Missing(ValueNames.ConnectionString, identifier, values),
            QueueName = Text(values, AzureEnvironmentResourceKinds.QueueNameValue),
            TopicName = Text(values, AzureEnvironmentResourceKinds.TopicNameValue),
            SubscriptionName = Text(values, AzureEnvironmentResourceKinds.SubscriptionNameValue),
            RequiredSession = Flag(values, AzureEnvironmentResourceKinds.RequiredSessionValue, identifier, fallback: false),
        };
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
