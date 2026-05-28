using Azure.Messaging.ServiceBus;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.ServiceBus;

/// <summary>
/// Step result context for sent Service Bus messages.
/// </summary>
/// <param name="Message">The message that was sent.</param>
public sealed record ServiceBusSentMessageContext(ServiceBusMessage Message) : StepResultContext;