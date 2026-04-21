using System;

namespace TestFramework.Azure.Configuration.SpecificConfigs;

public enum ServiceBusConfigReceiveMode
{
    Queue,
    TopicSubscription,
    TopicTemporarySubscription,
}

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

    public string EntityName
    {
        get
        {
            if (!string.IsNullOrEmpty(QueueName)) return QueueName;
            if (!string.IsNullOrEmpty(TopicName)) return TopicName;
            throw new InvalidOperationException("Either QueueName or TopicName must be provided.");
        }
    }

    public bool IsTopic => !string.IsNullOrEmpty(TopicName);

    public bool IsQueue => !string.IsNullOrEmpty(QueueName);

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

    public string SubscriptionNameRequired => SubscriptionName ?? throw new InvalidOperationException("SubscriptionName must be provided for topic-backed receive unless createTempSubscription is enabled.");
}