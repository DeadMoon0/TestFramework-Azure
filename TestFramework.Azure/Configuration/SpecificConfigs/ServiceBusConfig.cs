using System;

namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// Describes which Service Bus receive flow a configuration supports.
/// </summary>
public enum ServiceBusConfigReceiveMode
{
    /// <summary>
    /// Receive directly from a queue.
    /// </summary>
    Queue,

    /// <summary>
    /// Receive from a topic using a fixed subscription.
    /// </summary>
    TopicSubscription,

    /// <summary>
    /// Receive from a topic using a temporary subscription created by the framework.
    /// </summary>
    TopicTemporarySubscription,
}

/// <summary>
/// Configuration required to send to or receive from Azure Service Bus.
/// </summary>
/// <remarks>
/// The identifier maps to a named entry under the <c>ServiceBus</c> section.
/// Configure either <see cref="QueueName"/> for queue-backed usage or <see cref="TopicName"/> for topic-backed usage, but not both.
/// </remarks>
public sealed record ServiceBusConfig
{
    // Its text redacts its credentials; see ConfigRecordText.
    private bool PrintMembers(System.Text.StringBuilder builder) => ConfigRecordText.PrintMembers(this, builder);

    /// <summary>
    /// Shared connection string for the Service Bus namespace.
    /// </summary>
    [Secret]
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Queue-backed mode. Required for queue send and receive flows.
    /// Leave <see cref="TopicName"/> and <see cref="SubscriptionName"/> unset in this mode.
    /// </summary>
    public string? QueueName { get; init; }

    /// <summary>
    /// Topic-backed mode. Required for topic send and receive flows.
    /// Pair with <see cref="SubscriptionName"/> for a fixed subscription, or use
    /// AzureExt.Event.ServiceBus.MessageReceived(...).WithTempSubscription() for a temp subscription.
    /// </summary>
    public string? TopicName { get; init; }

    /// <summary>
    /// Fixed topic subscription for receive flows. Not used for queues and optional when a temp subscription is created.
    /// </summary>
    public string? SubscriptionName { get; init; }

    /// <summary>
    /// When true, send and receive flows use session-capable Service Bus clients.
    /// </summary>
    public bool RequiresSession { get; init; }

    /// <summary>
    /// Gets the active queue or topic entity name.
    /// </summary>
    /// <exception cref="TestFramework.Core.Exceptions.FrameworkConfigurationException">Thrown when neither <see cref="QueueName"/> nor <see cref="TopicName"/> is configured.</exception>
    public string EntityName
    {
        get
        {
            if (!string.IsNullOrEmpty(QueueName)) return QueueName;
            if (!string.IsNullOrEmpty(TopicName)) return TopicName;
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException("Either QueueName or TopicName must be provided.");
        }
    }

    /// <summary>
    /// Refuses an entry that names both a queue and a topic.
    /// </summary>
    /// <remarks>
    /// Both readers call this - the settings file's and the run's - so a record built in code is held to it too: a run
    /// reads every record back through its shape. With both names set, the receive mode said queue while the receiver
    /// was built for the topic, and the wait failed with a bare SDK error about a null subscription. An entry naming
    /// neither is a namespace - a container environment declares one - and is refused where an entity is needed.
    /// </remarks>
    /// <param name="identifier">The entry's identifier, for the message.</param>
    /// <returns>This record.</returns>
    internal ServiceBusConfig EnsureNamesOneEntity(string identifier)
    {
        if (IsQueue && IsTopic)
        {
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"Service Bus entry '{identifier}' names both a queue ('{QueueName}') and a topic ('{TopicName}'); an entry addresses one entity.")
            {
                RecoverySteps = ["Keep QueueName or TopicName, and declare the other as an entry of its own."],
            };
        }

        return this;
    }

    /// <summary>
    /// Gets a value indicating whether this configuration targets a topic.
    /// </summary>
    public bool IsTopic => !string.IsNullOrEmpty(TopicName);

    /// <summary>
    /// Gets a value indicating whether this configuration targets a queue.
    /// </summary>
    public bool IsQueue => !string.IsNullOrEmpty(QueueName);

    /// <summary>
    /// Resolves the receive mode for the current configuration.
    /// </summary>
    /// <param name="createTempSubscription">Whether the framework should create a temporary subscription for topic-backed receive flows.</param>
    /// <returns>The receive mode implied by the configured entity fields.</returns>
    internal ServiceBusConfigReceiveMode GetReceiveMode(bool createTempSubscription)
    {
        if (IsQueue) return ServiceBusConfigReceiveMode.Queue;
        if (!IsTopic)
        {
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException("Either QueueName or TopicName must be provided.");
        }

        if (createTempSubscription)
        {
            return ServiceBusConfigReceiveMode.TopicTemporarySubscription;
        }

        if (!string.IsNullOrEmpty(SubscriptionName))
        {
            return ServiceBusConfigReceiveMode.TopicSubscription;
        }

        throw new TestFramework.Core.Exceptions.FrameworkConfigurationException("SubscriptionName must be provided for topic-backed receive unless createTempSubscription is enabled.");
    }

    /// <summary>
    /// Gets the configured subscription name or throws when a topic-backed fixed subscription has not been configured.
    /// </summary>
    public string SubscriptionNameRequired => SubscriptionName ?? throw new TestFramework.Core.Exceptions.FrameworkConfigurationException("SubscriptionName must be provided for topic-backed receive unless createTempSubscription is enabled.");
}