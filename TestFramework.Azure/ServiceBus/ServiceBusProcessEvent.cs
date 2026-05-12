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
using TestFramework.Core.Events;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.ServiceBus;

/// <summary>
/// Async event that waits for a matching Azure Service Bus message.
/// </summary>
/// <param name="identifier">The Service Bus identifier to resolve.</param>
/// <param name="messageId">Optional message id filter.</param>
/// <param name="correlationId">Optional correlation id filter.</param>
/// <param name="predicate">Optional custom predicate applied to received messages.</param>
/// <param name="completeMessage">Whether matching messages should be completed automatically.</param>
/// <param name="createTempSubscription">Whether a temporary subscription should be created for topic-backed receive flows.</param>
public class ServiceBusProcessEvent(
    ServiceBusIdentifier identifier,
    VariableReference<string>? messageId = null,
    VariableReference<string>? correlationId = null,
    VariableReference<Func<ServiceBusReceivedMessage, bool>>? predicate = null,
    VariableReference<bool>? completeMessage = null,
    VariableReference<bool>? createTempSubscription = null)
    : AsyncEvent<ServiceBusProcessEvent, ServiceBusReceivedMessage>, IHasPreStep, IHasCleanupStep, IHasEnvironmentRequirements
{
    /// <summary>
    /// Gets the display name used for this event.
    /// </summary>
    public override string Name => "ServiceBus Process Event";
    /// <summary>
    /// Gets the event description.
    /// </summary>
    public override string Description => "An event that is triggered when a message is received from a Service Bus.";
    /// <summary>
    /// Gets a value indicating whether this event returns a value.
    /// </summary>
    public override bool DoesReturn => true;

    /// <summary>
    /// Declares the event inputs required by the configured filters.
    /// </summary>
    /// <param name="contract">The I/O contract being populated.</param>
    public override void DeclareIO(StepIOContract contract)
    {
        if (messageId is not null && messageId.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(messageId.Identifier!.Identifier, StepIOKind.Variable, false, typeof(string)));
        if (correlationId is not null && correlationId.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(correlationId.Identifier!.Identifier, StepIOKind.Variable, false, typeof(string)));
        if (predicate is not null && predicate.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(predicate.Identifier!.Identifier, StepIOKind.Variable, false));
        if (createTempSubscription is not null && createTempSubscription.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(createTempSubscription.Identifier!.Identifier, StepIOKind.Variable, false, typeof(bool)));
    }

    /// <summary>
    /// Creates a copy of this event and its options.
    /// </summary>
    /// <returns>The cloned event.</returns>
    public override Step<ServiceBusReceivedMessage> Clone()
        => new ServiceBusProcessEvent(identifier, messageId, correlationId, predicate, completeMessage, createTempSubscription)
            .WithClonedOptions(this);

    // Generated on first access during pre-step phase; stable for the lifetime of this clone.
    private string _tempSubName = "";
    private string TempSubscriptionName
    {
        get
        {
            if (_tempSubName.Length == 0)
                _tempSubName = "tmp-" + Guid.NewGuid().ToString("N")[..12];
            return _tempSubName;
        }
    }

    // ── IHasPreStep ───────────────────────────────────────────────────────────
    StepGeneric? IHasPreStep.CreatePreStep(VariableStore variableStore)
    {
        if (createTempSubscription?.GetValue(variableStore) != true) return null;
        var timeout = TimeOutOptions.TimeOut.GetValue(null!);
        return new ServiceBusCreateTempSubscriptionStep(
            identifier, TempSubscriptionName, messageId, correlationId, timeout);
    }

    // ── IHasCleanupStep ───────────────────────────────────────────────────────
    StepGeneric? IHasCleanupStep.CreateCleanupStep(VariableStore variableStore)
    {
        if (createTempSubscription?.GetValue(variableStore) != true) return null;
        return new ServiceBusDeleteTempSubscriptionStep(identifier, TempSubscriptionName);
    }

    /// <summary>
    /// Declares the environment requirement for the configured Service Bus resource.
    /// </summary>
    /// <param name="variableStore">The variable store for the current run.</param>
    /// <returns>The required Azure environment resources.</returns>
    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.ServiceBus, identifier)];


    // ── DoEventPolling ────────────────────────────────────────────────────────
    /// <summary>
    /// Polls Service Bus until a matching message is received.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve Azure services.</param>
    /// <param name="variableStore">The variable store used to resolve filters.</param>
    /// <param name="artifactStore">The artifact store for the current run.</param>
    /// <param name="logger">The event logger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching Service Bus message, or <see langword="null"/> when none is found.</returns>
    public override async Task<ServiceBusReceivedMessage?> DoEventPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore,
        ScopedLogger logger, CancellationToken cancellationToken)
    {
        ServiceBusConfig config = serviceProvider.GetRequiredService<ConfigStore<ServiceBusConfig>>().GetConfig(identifier);
        bool useTempSubscription = createTempSubscription?.GetValue(variableStore) == true;

        string? subscriptionName = config.GetReceiveMode(useTempSubscription) switch
        {
            ServiceBusConfigReceiveMode.Queue => null,
            ServiceBusConfigReceiveMode.TopicSubscription => config.SubscriptionNameRequired,
            ServiceBusConfigReceiveMode.TopicTemporarySubscription => TempSubscriptionName,
            _ => throw new InvalidOperationException($"Unsupported Service Bus receive mode for '{identifier}'."),
        };

        await using IServiceBusMessagePump pump = serviceProvider.GetAzureComponentFactory().ServiceBus.CreateMessagePump(config, subscriptionName);
        return await pump.ReceiveMessageAsync(
            new ServiceBusReceiveRequest(
                messageId?.GetValue(variableStore),
                correlationId?.GetValue(variableStore),
                predicate?.GetValue(variableStore),
                completeMessage?.GetValue(variableStore) == true),
            cancellationToken);
    }
}