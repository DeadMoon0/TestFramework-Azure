using Azure.Messaging.ServiceBus;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.ServiceBus;

/// <summary>
/// Step result context for received Service Bus messages.
/// </summary>
/// <param name="Message">The received Service Bus message.</param>
public sealed record ServiceBusReceivedMessageContext(ServiceBusReceivedMessage Message) : StepResultContext;