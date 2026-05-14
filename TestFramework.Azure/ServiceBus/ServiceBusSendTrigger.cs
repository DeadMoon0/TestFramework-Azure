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
public class ServiceBusSendTrigger(ServiceBusIdentifier identifier, VariableReference<ServiceBusMessage> message) : Step<EmptyStepResultContext>, IHasEnvironmentRequirements
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
    public override bool DoesReturn => false;

    /// <summary>
    /// Creates a copy of this trigger and its options.
    /// </summary>
    /// <returns>The cloned step.</returns>
    public override Step<EmptyStepResultContext> Clone()
    {
        return new ServiceBusSendTrigger(identifier, message).WithClonedOptions(this);
    }

    /// <summary>
    /// Declares the step inputs required by the message variable.
    /// </summary>
    /// <param name="contract">The I/O contract being populated.</param>
    public override void DeclareIO(StepIOContract contract)
    {
        if (message.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(message.Identifier!.Identifier, StepIOKind.Variable, true, typeof(ServiceBusMessage)));
    }

    /// <summary>
    /// Sends the configured Service Bus message.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve Azure services.</param>
    /// <param name="variableStore">The variable store used to resolve the message.</param>
    /// <param name="artifactStore">The artifact store for the current run.</param>
    /// <param name="logger">The step logger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the message has been sent.</returns>
    public override async Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore,
        ScopedLogger logger, CancellationToken cancellationToken)
    {
        ServiceBusConfig config = serviceProvider.GetRequiredService<ConfigStore<ServiceBusConfig>>().GetConfig(identifier);
        await using IServiceBusSenderAdapter sender = serviceProvider.GetAzureComponentFactory().ServiceBus.CreateSender(config);

        ServiceBusMessage _message = message.GetRequiredValue(variableStore);
        if (config.RequiredSession && _message.SessionId is null)
        {
            _message.SessionId = Guid.NewGuid().ToString();
        }

        await sender.SendMessageAsync(_message, cancellationToken);

        logger.LogInformation("Sent message to Service Bus: {0}", _message);

        return null;
    }

    /// <summary>
    /// Creates a runtime step instance.
    /// </summary>
    /// <returns>The runtime step instance.</returns>
    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext>(this);

    /// <summary>
    /// Declares the environment requirement for the configured Service Bus resource.
    /// </summary>
    /// <param name="variableStore">The variable store for the current run.</param>
    /// <returns>The required Azure environment resources.</returns>
    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.ServiceBus, identifier)];
}