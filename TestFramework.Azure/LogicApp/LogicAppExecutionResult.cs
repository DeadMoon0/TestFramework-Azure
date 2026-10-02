using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Normalized Logic App execution result that exposes useful bindable outputs.
/// </summary>
public sealed record LogicAppExecutionResult : StepResultContext
{
    internal LogicAppExecutionResult(string workflowName, string? triggerName, string? invokeUri, string? runId, HttpStatusCode? statusCode, LogicAppRunStatus? runStatus, string? runCode, string? responseBody, IReadOnlyDictionary<string, string[]>? responseHeaders, string? outputsJson, string? rawRunJson)
    {
        this.WorkflowName = workflowName;
        this.TriggerName = triggerName;
        this.invokeUri = LogicAppUriRedaction.Redact(invokeUri);
        this.RunId = runId;
        this.StatusCode = statusCode;
        this.RunStatus = runStatus;
        this.RunCode = runCode;
        this.ResponseBody = responseBody;
        this.ResponseHeaders = responseHeaders;
        this.OutputsJson = outputsJson;
        this.RawRunJson = rawRunJson;
    }

    /// <summary>
    /// The resolved workflow name.
    /// </summary>
    public string WorkflowName { get; }

    /// <summary>
    /// The resolved trigger name, when available.
    /// </summary>
    public string? TriggerName { get; }


    /// <summary>
    /// The resolved workflow run identifier, when available.
    /// </summary>
    public string? RunId { get; }

    /// <summary>
    /// The HTTP status returned by the invoke or trigger request, when available.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// The resolved workflow run status, when available.
    /// </summary>
    public LogicAppRunStatus? RunStatus { get; }

    /// <summary>
    /// The runtime code reported by the workflow, when available.
    /// </summary>
    public string? RunCode { get; }

    /// <summary>
    /// The direct HTTP response body, when captured.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// The direct HTTP response headers, when captured.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? ResponseHeaders { get; }

    /// <summary>
    /// Serialized workflow outputs, when available.
    /// </summary>
    public string? OutputsJson { get; }

    /// <summary>
    /// The raw management payload used to build run details, when available.
    /// </summary>
    public string? RawRunJson { get; }

    private readonly string? invokeUri;

    /// <summary>
    /// The invoke or management URI used for the interaction, with any shared-access signature
    /// redacted.
    /// </summary>
    /// <remarks>
    /// Redaction happens here rather than at each call site because this record is rendered into
    /// logs, reports and the DebugUI by <c>ToString()</c>. Redaction is idempotent, so results built
    /// from an already-redacted URL are unaffected.
    /// </remarks>
    public string? InvokeUri => invokeUri;

    /// <summary>
    /// Creates an explicit run-tracking context for stateful Logic App polling.
    /// </summary>
    internal LogicAppRunContext RunContext => new(
        WorkflowName,
        !string.IsNullOrWhiteSpace(RunId)
            ? RunId
            : throw new TestFramework.Core.Exceptions.FrameworkStateException("The Logic App execution result does not contain a workflow run id."));

    /// <summary>
    /// Formats only the persisted execution fields and avoids traversing derived contexts.
    /// </summary>
    protected override bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"WorkflowName = {WorkflowName}");
        builder.Append($", TriggerName = {TriggerName}");
        builder.Append($", InvokeUri = {InvokeUri}");
        builder.Append($", RunId = {RunId}");
        builder.Append($", StatusCode = {StatusCode}");
        builder.Append($", RunStatus = {RunStatus}");
        builder.Append($", RunCode = {RunCode}");
        builder.Append($", ResponseBody = {ResponseBody}");
        builder.Append($", ResponseHeaders = {ResponseHeaders}");
        builder.Append($", OutputsJson = {OutputsJson}");
        builder.Append($", RawRunJson = {RawRunJson}");
        return true;
    }
}