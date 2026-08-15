using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Normalized Logic App execution result that exposes useful bindable outputs.
/// </summary>
/// <param name="WorkflowName">The resolved workflow name.</param>
/// <param name="TriggerName">The resolved trigger name, when available.</param>
/// <param name="InvokeUri">The invoke or management URI used for the interaction, when available.</param>
/// <param name="RunId">The resolved workflow run identifier, when available.</param>
/// <param name="StatusCode">The HTTP status returned by the invoke or trigger request, when available.</param>
/// <param name="RunStatus">The resolved workflow run status, when available.</param>
/// <param name="RunCode">The runtime code reported by the workflow, when available.</param>
/// <param name="ResponseBody">The direct HTTP response body, when captured.</param>
/// <param name="ResponseHeaders">The direct HTTP response headers, when captured.</param>
/// <param name="OutputsJson">Serialized workflow outputs, when available.</param>
/// <param name="RawRunJson">The raw management payload used to build run details, when available.</param>
public sealed record LogicAppExecutionResult(
    string WorkflowName,
    string? TriggerName,
    string? InvokeUri,
    string? RunId,
    HttpStatusCode? StatusCode,
    LogicAppRunStatus? RunStatus,
    string? RunCode,
    string? ResponseBody,
    IReadOnlyDictionary<string, string[]>? ResponseHeaders,
    string? OutputsJson,
    string? RawRunJson) : StepResultContext
{
    private readonly string? invokeUri = LogicAppUriRedaction.Redact(InvokeUri);

    /// <summary>
    /// The invoke or management URI used for the interaction, with any shared-access signature
    /// redacted.
    /// </summary>
    /// <remarks>
    /// Redaction happens here rather than at each call site because this record is rendered into
    /// logs, reports and the DebugUI by <c>ToString()</c>. Redaction is idempotent, so results built
    /// from an already-redacted URL are unaffected.
    /// </remarks>
    public string? InvokeUri
    {
        get => invokeUri;
        init => invokeUri = LogicAppUriRedaction.Redact(value);
    }

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