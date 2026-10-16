using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TestFramework.Azure.ServiceBus;

/// <summary>
/// What a Service Bus send trigger sent, frozen at the moment it was sent.
/// </summary>
/// <remarks>
/// A finished run's record must not change afterwards, so this holds its own copy of the body bytes and the
/// application properties instead of the live <see cref="ServiceBusMessage"/>, which anyone holding it could
/// still change.
/// </remarks>
public sealed class ServiceBusSentMessage
{
    private ServiceBusSentMessage(ServiceBusMessage sent)
    {
        this.MessageId = sent.MessageId;
        this.CorrelationId = sent.CorrelationId;
        this.SessionId = sent.SessionId;
        this.PartitionKey = sent.PartitionKey;
        this.Subject = sent.Subject;
        this.ContentType = sent.ContentType;
        this.ReplyTo = sent.ReplyTo;
        this.ReplyToSessionId = sent.ReplyToSessionId;
        this.To = sent.To;
        this.TimeToLive = sent.TimeToLive;
        this.ScheduledEnqueueTime = sent.ScheduledEnqueueTime;
        this.Body = sent.GetRawAmqpMessage().Body.BodyType == AmqpMessageBodyType.Data
            ? BinaryData.FromBytes(sent.Body.ToArray())
            : null;
        this.ApplicationProperties = new ReadOnlyDictionary<string, object>(
            sent.ApplicationProperties.ToDictionary(pair => pair.Key, pair => pair.Value is byte[] bytes ? bytes.Clone() : pair.Value));
    }

    /// <summary>The message id.</summary>
    public string MessageId { get; }

    /// <summary>The correlation id.</summary>
    public string CorrelationId { get; }

    /// <summary>The session id, including one the trigger assigned for a session-enabled entity.</summary>
    public string SessionId { get; }

    /// <summary>The partition key.</summary>
    public string PartitionKey { get; }

    /// <summary>The subject.</summary>
    public string Subject { get; }

    /// <summary>The content type.</summary>
    public string ContentType { get; }

    /// <summary>The reply-to address.</summary>
    public string ReplyTo { get; }

    /// <summary>The reply-to session id.</summary>
    public string ReplyToSessionId { get; }

    /// <summary>The destination.</summary>
    public string To { get; }

    /// <summary>The time to live.</summary>
    public TimeSpan TimeToLive { get; }

    /// <summary>The scheduled enqueue time.</summary>
    public DateTimeOffset ScheduledEnqueueTime { get; }

    /// <summary>
    /// A copy of the body, or <see langword="null"/> when the message carried an AMQP value or sequence body
    /// rather than data.
    /// </summary>
    public BinaryData? Body { get; }

    /// <summary>A copy of the application properties.</summary>
    public IReadOnlyDictionary<string, object> ApplicationProperties { get; }

    /// <summary>
    /// Freezes what was sent.
    /// </summary>
    /// <param name="sent">The message as it went to Service Bus.</param>
    /// <returns>A snapshot that does not follow later changes to <paramref name="sent"/>.</returns>
    internal static ServiceBusSentMessage Of(ServiceBusMessage sent) => new(sent);
}
