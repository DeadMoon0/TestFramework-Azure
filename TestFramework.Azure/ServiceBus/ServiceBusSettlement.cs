using System;
using Azure.Messaging.ServiceBus;
using TestFramework.Azure.Runtime;

namespace TestFramework.Azure.ServiceBus;

/// <summary>
/// What a wait does with a delivered message.
/// </summary>
internal enum ServiceBusSettlementAction
{
    /// <summary>Not the awaited message, and left alone: a temporary subscription only delivers matches anyway.</summary>
    Ignore,

    /// <summary>Not the awaited message, and released at once, so a concurrent run's wait sees it.</summary>
    Abandon,

    /// <summary>The awaited message, completed before the wait returns.</summary>
    Complete,

    /// <summary>The awaited message, left on the entity because the wait said so.</summary>
    AcceptWithoutCompleting,
}

/// <summary>
/// The decisions a Service Bus wait makes about each delivery and each processor error, apart from the processor
/// that delivers them.
/// </summary>
/// <remarks>
/// Pure functions, so the rules that decide whether a run passes - which message is the one, what happens to the
/// others, which error ends the wait - are tested without a broker.
/// </remarks>
internal static class ServiceBusSettlement
{
    /// <summary>
    /// Whether a delivered message is the one the wait asked for.
    /// </summary>
    /// <param name="request">The wait's filters.</param>
    /// <param name="messageId">The message's id.</param>
    /// <param name="correlationId">The message's correlation id.</param>
    /// <param name="predicate">Reads the message for the wait's own predicate.</param>
    /// <returns>Whether every filter the wait states accepts it.</returns>
    internal static bool Matches(ServiceBusReceiveRequest request, string? messageId, string? correlationId, Func<Func<ServiceBusReceivedMessage, bool>, bool> predicate)
    {
        if (request.MessageId is { } wantedId && messageId != wantedId) return false;
        if (request.CorrelationId is { } wantedCorrelation && correlationId != wantedCorrelation) return false;
        if (request.Predicate is { } wanted && !predicate(wanted)) return false;
        return true;
    }

    /// <summary>
    /// What to do with a delivered message.
    /// </summary>
    /// <param name="request">The wait's settlement rules.</param>
    /// <param name="matches">Whether the message is the awaited one.</param>
    /// <returns>The action.</returns>
    internal static ServiceBusSettlementAction Decide(ServiceBusReceiveRequest request, bool matches)
    {
        if (!matches)
            return request.AbandonNonMatching ? ServiceBusSettlementAction.Abandon : ServiceBusSettlementAction.Ignore;

        return request.CompleteMessage ? ServiceBusSettlementAction.Complete : ServiceBusSettlementAction.AcceptWithoutCompleting;
    }

    /// <summary>
    /// Whether a processor error ends the wait.
    /// </summary>
    /// <remarks>
    /// The processor reports every error it meets, including the transient ones it retries by itself - a dropped link,
    /// a busy namespace. Failing the wait on one of those failed a run the broker was about to serve.
    /// </remarks>
    /// <param name="exception">The reported error.</param>
    /// <returns><see langword="false"/> for an error the processor recovers from.</returns>
    internal static bool EndsTheWait(Exception exception) => exception is not ServiceBusException { IsTransient: true };
}
