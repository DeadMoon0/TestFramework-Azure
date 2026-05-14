using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
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

internal class ServiceBusDeleteTempSubscriptionStep(
    ServiceBusIdentifier identifier,
    string tempSubscriptionName) : Step<EmptyStepResultContext>
{
    public override string Name => "ServiceBus Delete Temp Subscription";
    public override string Description => $"Deletes the temporary subscription '{tempSubscriptionName}'.";
    public override bool DoesReturn => false;

    public override Step<EmptyStepResultContext> Clone() =>
        new ServiceBusDeleteTempSubscriptionStep(identifier, tempSubscriptionName)
            .WithClonedOptions(this);

    public override void DeclareIO(StepIOContract contract) { }

    public override async Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore,
        ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ServiceBusConfig config = serviceProvider.GetRequiredService<ConfigStore<ServiceBusConfig>>().GetConfig(identifier);

        IServiceBusAdministrationAdapter adminClient = serviceProvider.GetAzureComponentFactory().ServiceBus.CreateAdministration(config);
        try
        {
            string topicName = config.TopicName ?? throw new InvalidOperationException("A topic name is required for deleting a temporary subscription.");
            await adminClient.DeleteSubscriptionAsync(topicName, tempSubscriptionName, cancellationToken);
            logger.LogInformation($"Deleted temp subscription '{tempSubscriptionName}' from topic '{topicName}'.");
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            // Already gone — nothing to do.
            logger.LogInformation($"Temp subscription '{tempSubscriptionName}' was already deleted.");
        }

        return null;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() =>
        new StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext>(this);
}
