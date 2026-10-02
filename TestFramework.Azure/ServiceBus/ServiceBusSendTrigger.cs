using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.ServiceBus;

/// <summary>
/// Timeline step that sends a message to the configured Azure Service Bus entity.
/// </summary>
/// <param name="identifier">The Service Bus identifier to resolve.</param>
/// <param name="message">The message variable to send.</param>
public class ServiceBusSendTrigger(ServiceBusIdentifier identifier, VariableReference<ServiceBusMessage> message) : Step<ServiceBusSentMessageContext>, IHasEnvironmentRequirements
{
    /// <summary>
    /// Gets the display name used for this step.
    /// </summary>
    public override string Name => "ServiceBus Send Trigger";
    /// <summary>
    /// Gets the step description.
    /// </summary>
    public override string Description => "Triggers a message to be sent to a Service Bus.";
    /// <summary>
    /// Gets a value indicating whether this step returns a value.
    /// </summary>
    public override bool DoesReturn => true;

    /// <summary>
    /// Creates a copy of this trigger and its options.
    /// </summary>
    /// <returns>The cloned step.</returns>
    public override Step<ServiceBusSentMessageContext> Clone()
    {
        return new ServiceBusSendTrigger(identifier, message).WithClonedOptions(this);
    }

    /// <summary>
    /// Declares the step inputs required by the message variable.
    /// </summary>
    /// <param name="contract">The I/O contract being populated.</param>
    public override void DeclareIO(StepIOContract contract)
    {
        contract.Input(message);
    }

    /// <summary>
    /// Sends the configured Service Bus message.
    /// </summary>
    /// <returns>A task that completes when the message has been sent.</returns>
    public override async Task<ServiceBusSentMessageContext?> Execute(RunContext context)
    {
        Resolved<ServiceBusConfig> serviceBus = AzureResources.For<ServiceBusConfig>(context, identifier);
        ServiceBusConfig config = serviceBus.Value;
        await using IServiceBusSenderAdapter sender = context.Services.GetAzureComponentFactory().ServiceBus.CreateSender(serviceBus);

        ServiceBusMessage _message = message.GetRequiredValue(context.Variables);
        if (config.RequiredSession && _message.SessionId is null)
        {
            _message.SessionId = Guid.NewGuid().ToString();
        }

        await sender.SendMessageAsync(_message, context.Deadline.Token);

        context.Logger.LogInformation("Sent message to Service Bus: {0}", _message);

        return new ServiceBusSentMessageContext(_message);
    }

    /// <summary>
    /// Creates a runtime step instance.
    /// </summary>
    /// <returns>The runtime step instance.</returns>
    public override StepInstance<Step<ServiceBusSentMessageContext>, ServiceBusSentMessageContext> GetInstance() => new StepInstance<Step<ServiceBusSentMessageContext>, ServiceBusSentMessageContext>(this);

    /// <summary>
    /// Declares the environment requirement for the configured Service Bus resource.
    /// </summary>
    /// <returns>The required Azure environment resources.</returns>
    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.ServiceBus, identifier)];
}