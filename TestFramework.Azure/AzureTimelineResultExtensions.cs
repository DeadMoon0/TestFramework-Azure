using System.Collections.Generic;
using System.Net;
using Azure.Messaging.ServiceBus;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Azure.LogicApp;
using TestFramework.Azure.ServiceBus;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.Azure;

/// <summary>
/// Typed result-binding helpers for Azure timeline steps.
/// </summary>
public static class AzureTimelineResultExtensions
{
    /// <summary>
    /// Binds the HTTP status code of a Function App HTTP call into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<HttpResponseResultContext> GetStatusCode(this ITimelineBuilderModifier<HttpResponseResultContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.StatusCode, identifier);

    /// <summary>
    /// Binds the response body of a Function App HTTP call into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<HttpResponseResultContext> GetBody(this ITimelineBuilderModifier<HttpResponseResultContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Body, identifier);

    /// <summary>
    /// Binds the response headers of a Function App HTTP call into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<HttpResponseResultContext> GetHeaders(this ITimelineBuilderModifier<HttpResponseResultContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Headers, identifier);

    /// <summary>
    /// Binds the status code of a managed Function App invocation into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ManagedResult> GetStatusCode(this ITimelineBuilderModifier<ManagedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.StatusCode, identifier);

    /// <summary>
    /// Binds the response body of a managed Function App invocation into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ManagedResult> GetBody(this ITimelineBuilderModifier<ManagedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Body, identifier);

    /// <summary>
    /// Binds the derived Logic App run context into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppTriggerResult> GetRunContext(this ITimelineBuilderModifier<LogicAppTriggerResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RunContext, identifier);

    /// <summary>
    /// Binds a direct Logic App run context step result into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunContext> GetRunContext(this ITimelineBuilderModifier<LogicAppRunContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Context, identifier);

    /// <summary>
    /// Binds the Logic App run id into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppTriggerResult> GetRunId(this ITimelineBuilderModifier<LogicAppTriggerResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RunId, identifier);

    /// <summary>
    /// Binds the Logic App trigger status code into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppTriggerResult> GetStatusCode(this ITimelineBuilderModifier<LogicAppTriggerResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.StatusCode, identifier);

    /// <summary>
    /// Binds the derived Logic App run context from a captured callback result into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppCapturedResult> GetRunContext(this ITimelineBuilderModifier<LogicAppCapturedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RunContext, identifier);

    /// <summary>
    /// Binds the Logic App captured callback status code into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppCapturedResult> GetStatusCode(this ITimelineBuilderModifier<LogicAppCapturedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.StatusCode, identifier);

    /// <summary>
    /// Binds the Logic App captured callback response body into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppCapturedResult> GetResponseBody(this ITimelineBuilderModifier<LogicAppCapturedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.ResponseBody, identifier);

    /// <summary>
    /// Binds the Logic App captured callback response headers into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppCapturedResult> GetResponseHeaders(this ITimelineBuilderModifier<LogicAppCapturedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.ResponseHeaders, identifier);

    /// <summary>
    /// Binds the received Service Bus message into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusReceivedMessageContext> GetMessage(this ITimelineBuilderModifier<ServiceBusReceivedMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message, identifier);

    /// <summary>
    /// Binds the received Service Bus message id into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusReceivedMessageContext> GetMessageId(this ITimelineBuilderModifier<ServiceBusReceivedMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.MessageId, identifier);

    /// <summary>
    /// Binds the received Service Bus correlation id into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusReceivedMessageContext> GetCorrelationId(this ITimelineBuilderModifier<ServiceBusReceivedMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.CorrelationId, identifier);

    /// <summary>
    /// Binds the received Service Bus message body into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusReceivedMessageContext> GetBody(this ITimelineBuilderModifier<ServiceBusReceivedMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.Body, identifier);

    /// <summary>
    /// Binds the polled Logic App run status into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunDetails> GetStatus(this ITimelineBuilderModifier<LogicAppRunDetails> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Status, identifier);

    /// <summary>
    /// Binds the serialized Logic App outputs JSON into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunDetails> GetOutputsJson(this ITimelineBuilderModifier<LogicAppRunDetails> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.OutputsJson, identifier);
}