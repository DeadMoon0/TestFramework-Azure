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
public record ServiceBusConfig
{
    /// <summary>
    /// Shared connection string for the Service Bus namespace.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Queue-backed mode. Required for queue send and receive flows.
    /// Leave <see cref="TopicName"/> and <see cref="SubscriptionName"/> unset in this mode.
    /// </summary>
    public required string? QueueName { get; init; }

    /// <summary>
    /// Topic-backed mode. Required for topic send and receive flows.
    /// Pair with <see cref="SubscriptionName"/> for a fixed subscription, or use
    /// AzureTF.Event.ServiceBus.MessageReceived(..., createTempSubscription: true) for a temp subscription.
    /// </summary>
    public required string? TopicName { get; init; }

    /// <summary>
    /// Fixed topic subscription for receive flows. Not used for queues and optional when a temp subscription is created.
    /// </summary>
    public required string? SubscriptionName { get; init; }

    /// <summary>
    /// When true, send and receive flows use session-capable Service Bus clients.
    /// </summary>
    public required bool RequiredSession { get; init; }

    /// <summary>
    /// Gets the active queue or topic entity name.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when neither <see cref="QueueName"/> nor <see cref="TopicName"/> is configured.</exception>
    public string EntityName
    {
        get
        {
            if (!string.IsNullOrEmpty(QueueName)) return QueueName;
            if (!string.IsNullOrEmpty(TopicName)) return TopicName;
            throw new InvalidOperationException("Either QueueName or TopicName must be provided.");
        }
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
    public ServiceBusConfigReceiveMode GetReceiveMode(bool createTempSubscription = false)
    {
        if (IsQueue) return ServiceBusConfigReceiveMode.Queue;
        if (!IsTopic)
        {
            throw new InvalidOperationException("Either QueueName or TopicName must be provided.");
        }

        if (createTempSubscription)
        {
            return ServiceBusConfigReceiveMode.TopicTemporarySubscription;
        }

        if (!string.IsNullOrEmpty(SubscriptionName))
        {
            return ServiceBusConfigReceiveMode.TopicSubscription;
        }

        throw new InvalidOperationException("SubscriptionName must be provided for topic-backed receive unless createTempSubscription is enabled.");
    }

    /// <summary>
    /// Gets the configured subscription name or throws when a topic-backed fixed subscription has not been configured.
    /// </summary>
    public string SubscriptionNameRequired => SubscriptionName ?? throw new InvalidOperationException("SubscriptionName must be provided for topic-backed receive unless createTempSubscription is enabled.");
}