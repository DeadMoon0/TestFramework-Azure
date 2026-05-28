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
    /// Binds the full Function App HTTP response context into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<HttpResponseResultContext> GetResponse(this ITimelineBuilderModifier<HttpResponseResultContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x, identifier);

    /// <summary>
    /// Binds the normalized Logic App execution result into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetExecutionResult(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x, identifier);

    /// <summary>
    /// Binds the resolved Logic App workflow name into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetWorkflowName(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.WorkflowName, identifier);

    /// <summary>
    /// Binds the resolved Logic App trigger name into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetTriggerName(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.TriggerName, identifier);

    /// <summary>
    /// Binds the invoke or management URI into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetInvokeUri(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.InvokeUri, identifier);

    /// <summary>
    /// Binds the Logic App run id into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetRunId(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RunId, identifier);

    /// <summary>
    /// Binds the Logic App run context into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetRunContext(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RunContext, identifier);

    /// <summary>
    /// Binds the Logic App trigger or invoke status code into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetStatusCode(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.StatusCode, identifier);

    /// <summary>
    /// Binds the Logic App run status into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetRunStatus(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RunStatus, identifier);

    /// <summary>
    /// Binds the Logic App run code into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetRunCode(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RunCode, identifier);

    /// <summary>
    /// Binds the Logic App response body into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetResponseBody(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.ResponseBody, identifier);

    /// <summary>
    /// Binds the Logic App response headers into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetResponseHeaders(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.ResponseHeaders, identifier);

    /// <summary>
    /// Binds the serialized Logic App outputs JSON into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetOutputsJson(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.OutputsJson, identifier);

    /// <summary>
    /// Binds the raw Logic App run payload into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppExecutionResult> GetRawRunJson(this ITimelineBuilderModifier<LogicAppExecutionResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RawRunJson, identifier);

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
    public static ITimelineBuilderModifier<ManagedResult> GetManagedResult(this ITimelineBuilderModifier<ManagedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x, identifier);

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
    /// Binds the normalized Logic App execution result into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppTriggerResult> GetExecutionResult(this ITimelineBuilderModifier<LogicAppTriggerResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.ExecutionResult, identifier);

    /// <summary>
    /// Binds the Logic App workflow name into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppTriggerResult> GetWorkflowName(this ITimelineBuilderModifier<LogicAppTriggerResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.WorkflowName, identifier);

    /// <summary>
    /// Binds the Logic App trigger name into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppTriggerResult> GetTriggerName(this ITimelineBuilderModifier<LogicAppTriggerResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.TriggerName, identifier);

    /// <summary>
    /// Binds the Logic App callback URL into a variable using the existing invoke URI naming.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppTriggerResult> GetInvokeUri(this ITimelineBuilderModifier<LogicAppTriggerResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.InvokeUri, identifier);

    /// <summary>
    /// Binds the Logic App callback URL into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppTriggerResult> GetCallbackUrl(this ITimelineBuilderModifier<LogicAppTriggerResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.CallbackUrl, identifier);

    /// <summary>
    /// Binds the derived Logic App run context into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppTriggerResult> GetRunContext(this ITimelineBuilderModifier<LogicAppTriggerResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RunContext, identifier);

    /// <summary>
    /// Binds the lightweight Logic App execution summary that can be reconstructed from the run context.
    /// Fields that require an HTTP response or management payload remain null until a richer result type is captured.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunContext> GetExecutionResult(this ITimelineBuilderModifier<LogicAppRunContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.ExecutionResult, identifier);

    /// <summary>
    /// Binds the lightweight Logic App execution summary that can be reconstructed from the run context.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunContext> GetRunSummary(this ITimelineBuilderModifier<LogicAppRunContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RunSummary, identifier);

    /// <summary>
    /// Binds the current Logic App run context instance into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunContext> GetRunContext(this ITimelineBuilderModifier<LogicAppRunContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Context, identifier);

    /// <summary>
    /// Binds the Logic App workflow name into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunContext> GetWorkflowName(this ITimelineBuilderModifier<LogicAppRunContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.WorkflowName, identifier);

    /// <summary>
    /// Binds the Logic App run id into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunContext> GetRunId(this ITimelineBuilderModifier<LogicAppRunContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RunId, identifier);

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
    /// Binds the normalized Logic App execution result into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppCapturedResult> GetExecutionResult(this ITimelineBuilderModifier<LogicAppCapturedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.ExecutionResult, identifier);

    /// <summary>
    /// Binds the Logic App workflow name into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppCapturedResult> GetWorkflowName(this ITimelineBuilderModifier<LogicAppCapturedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.WorkflowName, identifier);

    /// <summary>
    /// Binds the Logic App trigger name into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppCapturedResult> GetTriggerName(this ITimelineBuilderModifier<LogicAppCapturedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.TriggerName, identifier);

    /// <summary>
    /// Binds the Logic App callback URL into a variable using the existing invoke URI naming.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppCapturedResult> GetInvokeUri(this ITimelineBuilderModifier<LogicAppCapturedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.InvokeUri, identifier);

    /// <summary>
    /// Binds the Logic App callback URL into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppCapturedResult> GetCallbackUrl(this ITimelineBuilderModifier<LogicAppCapturedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.CallbackUrl, identifier);

    /// <summary>
    /// Binds the Logic App run id into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppCapturedResult> GetRunId(this ITimelineBuilderModifier<LogicAppCapturedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RunId, identifier);

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
    /// Binds the inferred Logic App run status into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppCapturedResult> GetRunStatus(this ITimelineBuilderModifier<LogicAppCapturedResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Status, identifier);

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
    /// Binds the full sent Service Bus message context into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusSentMessageContext> GetMessage(this ITimelineBuilderModifier<ServiceBusSentMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message, identifier);

    /// <summary>
    /// Binds the sent Service Bus message id into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusSentMessageContext> GetMessageId(this ITimelineBuilderModifier<ServiceBusSentMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.MessageId, identifier);

    /// <summary>
    /// Binds the sent Service Bus correlation id into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusSentMessageContext> GetCorrelationId(this ITimelineBuilderModifier<ServiceBusSentMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.CorrelationId, identifier);

    /// <summary>
    /// Binds the sent Service Bus message body into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusSentMessageContext> GetBody(this ITimelineBuilderModifier<ServiceBusSentMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.Body, identifier);

    /// <summary>
    /// Binds the sent Service Bus session id into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusSentMessageContext> GetSessionId(this ITimelineBuilderModifier<ServiceBusSentMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.SessionId, identifier);

    /// <summary>
    /// Binds the sent Service Bus subject into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusSentMessageContext> GetSubject(this ITimelineBuilderModifier<ServiceBusSentMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.Subject, identifier);

    /// <summary>
    /// Binds the sent Service Bus content type into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusSentMessageContext> GetContentType(this ITimelineBuilderModifier<ServiceBusSentMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.ContentType, identifier);

    /// <summary>
    /// Binds the sent Service Bus reply-to address into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusSentMessageContext> GetReplyTo(this ITimelineBuilderModifier<ServiceBusSentMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.ReplyTo, identifier);

    /// <summary>
    /// Binds the sent Service Bus reply-to session id into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusSentMessageContext> GetReplyToSessionId(this ITimelineBuilderModifier<ServiceBusSentMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.ReplyToSessionId, identifier);

    /// <summary>
    /// Binds the sent Service Bus destination into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusSentMessageContext> GetTo(this ITimelineBuilderModifier<ServiceBusSentMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.To, identifier);

    /// <summary>
    /// Binds the sent Service Bus application properties into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusSentMessageContext> GetApplicationProperties(this ITimelineBuilderModifier<ServiceBusSentMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.ApplicationProperties, identifier);

    /// <summary>
    /// Binds the sent Service Bus scheduled enqueue time into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<ServiceBusSentMessageContext> GetScheduledEnqueueTime(this ITimelineBuilderModifier<ServiceBusSentMessageContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Message.ScheduledEnqueueTime, identifier);

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
    /// Binds the full Azure liveness probe result into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> GetProbeResult(this ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x, identifier);

    /// <summary>
    /// Binds the Azure resource kind checked by an is-live trigger into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> GetResourceKind(this ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.ResourceKind, identifier);

    /// <summary>
    /// Binds the known typed Azure resource category into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> GetKnownResourceKind(this ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.KnownResourceKind, identifier);

    /// <summary>
    /// Binds the Azure resource identifier checked by an is-live trigger into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> GetResourceIdentifier(this ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.ResourceIdentifier, identifier);

    /// <summary>
    /// Binds the requested aliveness level into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> GetAlivenessLevel(this ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.AlivenessLevel, identifier);

    /// <summary>
    /// Binds the success flag from an Azure is-live probe into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> GetSuccess(this ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Success, identifier);

    /// <summary>
    /// Binds the optional HTTP status code from an Azure is-live probe into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> GetStatusCode(this ITimelineBuilderModifier<Trigger.IsLive.AzureIsLiveResult> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.StatusCode, identifier);

    /// <summary>
    /// Binds the normalized Logic App execution result into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunDetails> GetExecutionResult(this ITimelineBuilderModifier<LogicAppRunDetails> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.ExecutionResult, identifier);

    /// <summary>
    /// Binds the Logic App workflow name into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunDetails> GetWorkflowName(this ITimelineBuilderModifier<LogicAppRunDetails> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.WorkflowName, identifier);

    /// <summary>
    /// Binds the Logic App run id into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunDetails> GetRunId(this ITimelineBuilderModifier<LogicAppRunDetails> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RunId, identifier);

    /// <summary>
    /// Binds the polled Logic App run status into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunDetails> GetStatus(this ITimelineBuilderModifier<LogicAppRunDetails> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Status, identifier);

    /// <summary>
    /// Binds the runtime code reported by the Logic App run into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunDetails> GetRunCode(this ITimelineBuilderModifier<LogicAppRunDetails> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Code, identifier);

    /// <summary>
    /// Binds the serialized Logic App outputs JSON into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunDetails> GetOutputsJson(this ITimelineBuilderModifier<LogicAppRunDetails> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.OutputsJson, identifier);

    /// <summary>
    /// Binds the raw Logic App run payload into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<LogicAppRunDetails> GetRawRunJson(this ITimelineBuilderModifier<LogicAppRunDetails> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.RawJson, identifier);
}