using Azure.Messaging.ServiceBus;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.ServiceBus;

/// <summary>
/// Step result context for received Service Bus messages.
/// </summary>
public sealed record ServiceBusReceivedMessageContext : StepResultContext
{
    internal ServiceBusReceivedMessageContext(ServiceBusReceivedMessage message)
    {
        this.Message = message;
    }

    /// <summary>
    /// The received Service Bus message.
    /// </summary>
    public ServiceBusReceivedMessage Message { get; }
}