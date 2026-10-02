using Azure.Messaging.ServiceBus;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.ServiceBus;

/// <summary>
/// Step result context for sent Service Bus messages.
/// </summary>
public sealed record ServiceBusSentMessageContext : StepResultContext
{
    internal ServiceBusSentMessageContext(ServiceBusMessage message)
    {
        this.Message = message;
    }

    /// <summary>
    /// The message that was sent.
    /// </summary>
    public ServiceBusMessage Message { get; }
}