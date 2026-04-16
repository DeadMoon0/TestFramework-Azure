using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Events;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.ServiceBus;

public class ServiceBusProcessEvent(
    ServiceBusIdentifier identifier,
    VariableReference<string>? messageId = null,
    VariableReference<string>? correlationId = null,
    VariableReference<Func<ServiceBusReceivedMessage, bool>>? predicate = null,
    VariableReference<bool>? completeMessage = null,
    VariableReference<bool>? createTempSubscription = null)
    : AsyncEvent<ServiceBusProcessEvent, ServiceBusReceivedMessage>, IHasPreStep, IHasCleanupStep
{
    public override string Name => "ServiceBus Process Event";
    public override string Description => "An event that is triggered when a message is received from a Service Bus.";
    public override bool DoesReturn => true;

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


    // ── DoEventPolling ────────────────────────────────────────────────────────
    public override async Task<ServiceBusReceivedMessage?> DoEventPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore,
        ScopedLogger logger, CancellationToken cancellationToken)
    {
        ServiceBusConfig config = serviceProvider.GetRequiredService<ConfigStore<ServiceBusConfig>>().GetConfig(identifier);

        // When a temp subscription was created, use it; otherwise fall back to the configured one.
        string subscriptionName = createTempSubscription?.GetValue(variableStore) == true
            ? TempSubscriptionName
            : config.SubscriptionNameRequired;

        await using ServiceBusClient client = new ServiceBusClient(config.ConnectionString);
        if (config.RequiredSession)
        {
            ServiceBusSessionProcessor processor;
            if (config.IsTopic) processor = client.CreateSessionProcessor(config.TopicName, subscriptionName, new ServiceBusSessionProcessorOptions { AutoCompleteMessages = false });
            else processor = client.CreateSessionProcessor(config.EntityName, new ServiceBusSessionProcessorOptions { AutoCompleteMessages = false });

            TaskCompletionSource<ServiceBusReceivedMessage> tcs = new();

            processor.ProcessMessageAsync += async args =>
            {
                if (messageId?.GetValue(variableStore) is { } _messageId && args.Message.MessageId != _messageId) return;
                if (correlationId?.GetValue(variableStore) is { } _correlationId && args.Message.CorrelationId != _correlationId) return;
                if (predicate?.GetValue(variableStore) is { } _predicate && _predicate(args.Message)) return;
                tcs.SetResult(args.Message);
                if (completeMessage?.GetValue(variableStore) == true)
                    await args.CompleteMessageAsync(args.Message, cancellationToken);
            };

            processor.ProcessErrorAsync += args =>
            {
                tcs.SetException(args.Exception);
                return Task.CompletedTask;
            };

            await processor.StartProcessingAsync(cancellationToken);
            ServiceBusReceivedMessage message = await tcs.Task.WaitAsync(cancellationToken);
            await processor.DisposeAsync();
            return message;
        }
        else
        {
            ServiceBusProcessor processor;
            if (config.IsTopic) processor = client.CreateProcessor(config.TopicName, subscriptionName, new ServiceBusProcessorOptions { AutoCompleteMessages = false });
            else processor = client.CreateProcessor(config.EntityName, new ServiceBusProcessorOptions { AutoCompleteMessages = false });

            TaskCompletionSource<ServiceBusReceivedMessage> tcs = new();

            processor.ProcessMessageAsync += async args =>
            {
                if (messageId?.GetValue(variableStore) is { } _messageId && args.Message.MessageId != _messageId) return;
                if (correlationId?.GetValue(variableStore) is { } _correlationId && args.Message.CorrelationId != _correlationId) return;
                if (predicate?.GetValue(variableStore) is { } _predicate && _predicate(args.Message)) return;
                tcs.SetResult(args.Message);
                if (completeMessage?.GetValue(variableStore) == true)
                    await args.CompleteMessageAsync(args.Message, cancellationToken);
            };

            processor.ProcessErrorAsync += args =>
            {
                tcs.SetException(args.Exception);
                return Task.CompletedTask;
            };

            await processor.StartProcessingAsync(cancellationToken);
            ServiceBusReceivedMessage message = await tcs.Task.WaitAsync(cancellationToken);
            await processor.DisposeAsync();
            return message;
        }
    }
}