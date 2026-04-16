using System;

namespace TestFramework.Azure.Configuration.SpecificConfigs;

public record ServiceBusConfig
{
    public required string ConnectionString { get; init; }
    public required string? QueueName { get; init; }
    public required string? TopicName { get; init; }
    public required string? SubscriptionName { get; init; }
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

    public string SubscriptionNameRequired => SubscriptionName ?? throw new InvalidOperationException("SubscriptionName must be provided.");
}