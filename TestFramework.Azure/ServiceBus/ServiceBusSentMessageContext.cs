using TestFramework.Core.Steps;

namespace TestFramework.Azure.ServiceBus;

/// <summary>
/// Step result context for sent Service Bus messages.
/// </summary>
public sealed record ServiceBusSentMessageContext : StepResultContext
{
    internal ServiceBusSentMessageContext(ServiceBusSentMessage message)
    {
        this.Message = message;
    }

    /// <summary>
    /// The message that was sent, frozen when it was sent.
    /// </summary>
    public ServiceBusSentMessage Message { get; }
}