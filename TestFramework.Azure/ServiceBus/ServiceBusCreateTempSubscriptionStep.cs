using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.ServiceBus;

internal class ServiceBusCreateTempSubscriptionStep(
    ServiceBusIdentifier identifier,
    string tempSubscriptionName,
    VariableReference<string>? messageId,
    VariableReference<string>? correlationId,
    TimeSpan timeout) : Step<object?>
{
    public override string Name => "ServiceBus Create Temp Subscription";
    public override string Description => $"Creates a temporary subscription '{tempSubscriptionName}' for the duration of the test.";
    public override bool DoesReturn => false;

    public override Step<object?> Clone() =>
        new ServiceBusCreateTempSubscriptionStep(identifier, tempSubscriptionName, messageId, correlationId, timeout)
            .WithClonedOptions(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (messageId is not null && messageId.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(messageId.Identifier!.Identifier, StepIOKind.Variable, false, typeof(string)));
        if (correlationId is not null && correlationId.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(correlationId.Identifier!.Identifier, StepIOKind.Variable, false, typeof(string)));
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore,
        ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ServiceBusConfig config = serviceProvider.GetRequiredService<ConfigStore<ServiceBusConfig>>().GetConfig(identifier);

        if (!config.IsTopic)
            throw new InvalidOperationException(
                $"createTempSubscription is only supported for topics, not queues. Identifier: '{identifier}'");

        // TTL is at least 1 hour, never shorter than the step timeout.
        var ttl = timeout > TimeSpan.FromHours(1) ? timeout : TimeSpan.FromHours(1);

        var subOptions = new CreateSubscriptionOptions(config.TopicName, tempSubscriptionName)
        {
            DefaultMessageTimeToLive = ttl,
            AutoDeleteOnIdle = ttl + ttl,    // self-cleans if cleanup step doesn't run
            RequiresSession = config.RequiredSession,
        };

        // Build a server-side filter from the qualifiers that are known at pre-process time.
        // predicate stays in-process only — no server-side equivalent.
        var correlationIdValue = correlationId?.GetValue(variableStore);
        var messageIdValue = messageId?.GetValue(variableStore);

        CreateRuleOptions? ruleOptions = null;
        if (correlationIdValue is not null || messageIdValue is not null)
        {
            var filter = new CorrelationRuleFilter();
            if (correlationIdValue is not null) filter.CorrelationId = correlationIdValue;
            if (messageIdValue is not null) filter.MessageId = messageIdValue;
            ruleOptions = new CreateRuleOptions("TempFilter", filter);
        }

        var adminClient = new ServiceBusAdministrationClient(config.ConnectionString);

        if (ruleOptions is not null)
            await adminClient.CreateSubscriptionAsync(subOptions, ruleOptions, cancellationToken);
        else
            await adminClient.CreateSubscriptionAsync(subOptions, cancellationToken);

        logger.LogInformation(
            $"Created temp subscription '{tempSubscriptionName}' on topic '{config.TopicName}' (TTL {ttl}).");

        return null;
    }

    public override StepInstance<Step<object?>, object?> GetInstance() =>
        new StepInstance<Step<object?>, object?>(this);
}
