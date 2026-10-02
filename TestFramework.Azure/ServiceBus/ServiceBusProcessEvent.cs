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
/// <remarks>
/// Created by <c>AzureExt.Event.ServiceBus.MessageReceived(identifier)</c>, which matches any message, and narrowed
/// with <see cref="WithMessageId"/>, <see cref="WithCorrelationId"/> and <see cref="Where"/>. Each narrowing returns a
/// new event, so one that was handed to a timeline is never changed afterwards. A matching message is completed
/// unless <see cref="CompleteMessage"/> says otherwise.
/// </remarks>
public sealed class ServiceBusProcessEvent
    : AsyncEvent<ServiceBusProcessEvent, ServiceBusReceivedMessageContext>, IHasPreStep, IHasCleanupStep, IHasEnvironmentRequirements
{
    private readonly ServiceBusIdentifier identifier;
    private readonly VariableReference<string>? messageId;
    private readonly VariableReference<string>? correlationId;
    private readonly VariableReference<Func<ServiceBusReceivedMessage, bool>>? predicate;
    private readonly VariableReference<bool>? completeMessage;
    private readonly VariableReference<bool>? createTempSubscription;

    /// <summary>
    /// Created by <c>AzureExt.Event.ServiceBus.MessageReceived</c> and narrowed with the <c>With...</c> methods.
    /// </summary>
    internal ServiceBusProcessEvent(ServiceBusIdentifier identifier, VariableReference<string>? messageId = null, VariableReference<string>? correlationId = null, VariableReference<Func<ServiceBusReceivedMessage, bool>>? predicate = null, VariableReference<bool>? completeMessage = null, VariableReference<bool>? createTempSubscription = null)
    {
        this.identifier = identifier;
        this.messageId = messageId;
        this.correlationId = correlationId;
        this.predicate = predicate;
        this.completeMessage = completeMessage;
        this.createTempSubscription = createTempSubscription;
    }

    /// <summary>
    /// Waits only for the message with this id.
    /// </summary>
    /// <param name="messageId">The message id.</param>
    /// <returns>A narrowed copy of this event.</returns>
    public ServiceBusProcessEvent WithMessageId(VariableReference<string> messageId)
    {
        ArgumentNullException.ThrowIfNull(messageId);
        return new ServiceBusProcessEvent(identifier, messageId, correlationId, predicate, completeMessage, createTempSubscription);
    }

    /// <summary>
    /// Waits only for a message with this correlation id.
    /// </summary>
    /// <remarks>
    /// With <see cref="WithTempSubscription()"/>, the temporary subscription filters on it server-side.
    /// </remarks>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>A narrowed copy of this event.</returns>
    public ServiceBusProcessEvent WithCorrelationId(VariableReference<string> correlationId)
    {
        ArgumentNullException.ThrowIfNull(correlationId);
        return new ServiceBusProcessEvent(identifier, messageId, correlationId, predicate, completeMessage, createTempSubscription);
    }

    /// <summary>
    /// Waits only for a message the predicate accepts, on top of any id filters.
    /// </summary>
    /// <param name="predicate">The predicate.</param>
    /// <returns>A narrowed copy of this event.</returns>
    public ServiceBusProcessEvent Where(VariableReference<Func<ServiceBusReceivedMessage, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new ServiceBusProcessEvent(identifier, messageId, correlationId, predicate, completeMessage, createTempSubscription);
    }

    /// <summary>
    /// Whether the matching message is completed. Without this call it is.
    /// </summary>
    /// <remarks>
    /// <c>CompleteMessage(false)</c> leaves the message on the entity. It becomes visible again and can satisfy a
    /// later run's wait, which is why completing is what happens unless this says otherwise.
    /// </remarks>
    /// <param name="complete">Whether to complete it.</param>
    /// <returns>A copy of this event that settles the message that way.</returns>
    public ServiceBusProcessEvent CompleteMessage(VariableReference<bool> complete)
    {
        ArgumentNullException.ThrowIfNull(complete);
        return new ServiceBusProcessEvent(identifier, messageId, correlationId, predicate, complete, createTempSubscription);
    }

    /// <summary>
    /// Listens on a temporary subscription created for this wait, for a topic entry without one of its own.
    /// </summary>
    /// <remarks>
    /// A pre-step creates the subscription before the run's main stage, filtered on the id filters, and a cleanup
    /// step deletes it.
    /// </remarks>
    /// <returns>A copy of this event that creates its own subscription.</returns>
    public ServiceBusProcessEvent WithTempSubscription()
        => new ServiceBusProcessEvent(identifier, messageId, correlationId, predicate, completeMessage, Var.Const(true));

    /// <summary>
    /// Listens on a temporary subscription when the variable holds <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Immutable, because the decision is made when the run is planned: the pre-step that creates the subscription
    /// either exists or does not.
    /// </remarks>
    /// <typeparam name="TVar">The variable type.</typeparam>
    /// <param name="create">Whether to create one.</param>
    /// <returns>A copy of this event that creates its own subscription when asked to.</returns>
    public ServiceBusProcessEvent WithTempSubscription<TVar>(ImmutableVariable<TVar, bool> create)
        where TVar : VariableReference<bool>
    {
        ArgumentNullException.ThrowIfNull(create);
        return new ServiceBusProcessEvent(identifier, messageId, correlationId, predicate, completeMessage, create);
    }

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
        contract.OptionalInput(messageId);
        contract.OptionalInput(correlationId);
        contract.OptionalInput(predicate);
        contract.OptionalInput(createTempSubscription);
    }

    /// <summary>
    /// Creates a copy of this event and its options.
    /// </summary>
    /// <returns>The cloned event.</returns>
    public override Step<ServiceBusReceivedMessageContext> Clone()
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

    // Whether planning armed the temporary subscription, so polling listens on what was actually
    // created rather than re-asking a variable that may have changed since.
    private bool _tempSubscriptionArmed;

    // ── IHasPreStep ───────────────────────────────────────────────────────────
    StepGeneric? IHasPreStep.CreatePreStep(VariableStore variableStore)
    {
        if (createTempSubscription?.GetValue(variableStore) != true) return null;

        _tempSubscriptionArmed = true;

        // The store must be the real one: the default timeout happens to be a ConstVariable that
        // ignores it, but a variable-backed timeout would dereference null here.
        var timeout = TimeOutOptions.TimeOut.GetValue(variableStore);
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
    /// <returns>The required Azure environment resources.</returns>
    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.ServiceBus, identifier)];


    // ── DoEventPolling ────────────────────────────────────────────────────────
    /// <summary>
    /// Polls Service Bus until a matching message is received.
    /// </summary>
    /// <returns>The matching Service Bus message, or <see langword="null"/> when none is found.</returns>
    public override async Task<ServiceBusReceivedMessageContext?> DoEventPolling(RunContext context)
    {
        Resolved<ServiceBusConfig> serviceBus = AzureResources.For<ServiceBusConfig>(context, identifier);
        ServiceBusConfig config = serviceBus.Value;

        // The flag was already read at planning time, where it decided whether the create/delete
        // steps exist. A value that changed since would aim the pump at a subscription that was never
        // created (or ignore one that was), failing later as a bare SDK error - so a flip between the
        // two reads is refused here by name instead.
        bool useTempSubscription = createTempSubscription?.GetValue(context.Variables) == true;
        if (useTempSubscription != _tempSubscriptionArmed)
        {
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"'createTempSubscription' for '{identifier}' changed between planning and polling ({_tempSubscriptionArmed} -> {useTempSubscription}).")
            {
                RecoverySteps = ["The temporary subscription is created by a pre-step, so the flag must hold one value for the whole run."],
            };
        }

        ServiceBusConfigReceiveMode receiveMode = config.GetReceiveMode(useTempSubscription);
        string? subscriptionName = receiveMode switch
        {
            ServiceBusConfigReceiveMode.Queue => null,
            ServiceBusConfigReceiveMode.TopicSubscription => config.SubscriptionNameRequired,
            ServiceBusConfigReceiveMode.TopicTemporarySubscription => TempSubscriptionName,
            _ => throw new TestFramework.Core.Exceptions.UnsupportedFrameworkValueException($"Unsupported Service Bus receive mode for '{identifier}'."),
        };

        // A temporary subscription filters server-side, so anything delivered there matches by
        // construction. The shared modes see other runs' traffic and must release those locks.
        bool abandonNonMatching = receiveMode is ServiceBusConfigReceiveMode.Queue or ServiceBusConfigReceiveMode.TopicSubscription;

        await using IServiceBusMessagePump pump = context.Services.GetAzureComponentFactory().ServiceBus.CreateMessagePump(serviceBus, subscriptionName);
        ServiceBusReceivedMessage? message = await pump.ReceiveMessageAsync(
            new ServiceBusReceiveRequest(
                messageId?.GetValue(context.Variables),
                correlationId?.GetValue(context.Variables),
                predicate?.GetValue(context.Variables),
                // Unspecified means complete: a matched-but-uncompleted message returns to the entity
                // and can satisfy a LATER run's wait, which is a cross-run false pass.
                completeMessage?.GetValue(context.Variables) ?? true,
                abandonNonMatching,
                warning => context.Logger.LogWarning("ServiceBus '{0}': {1}", identifier, warning)),
            context.Deadline.Token);

        return message is null ? null : new ServiceBusReceivedMessageContext(message);
    }
}