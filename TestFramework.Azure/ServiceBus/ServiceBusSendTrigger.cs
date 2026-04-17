using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.ServiceBus;

public class ServiceBusSendTrigger(ServiceBusIdentifier identifier, VariableReference<ServiceBusMessage> message) : Step<object?>
{
    public override string Name => "ServiceBus Send Trigger";
    public override string Description => "Triggers a message to be sent to a Service Bus.";
    public override bool DoesReturn => false;

    public override Step<object?> Clone()
    {
        return new ServiceBusSendTrigger(identifier, message).WithClonedOptions(this);
    }

    public override void DeclareIO(StepIOContract contract)
    {
        if (message.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(message.Identifier!.Identifier, StepIOKind.Variable, true, typeof(ServiceBusMessage)));
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore,
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

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);
}