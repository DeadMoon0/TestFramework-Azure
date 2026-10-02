using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Result returned when a stateless Logic App manual trigger is invoked and its callback response is captured directly.
/// </summary>
public sealed record LogicAppCapturedResult : StepResultContext
{
    internal LogicAppCapturedResult(string workflowName, string triggerName, string callbackUrl, string? runId, HttpStatusCode statusCode, LogicAppRunStatus status, string responseBody, IReadOnlyDictionary<string, string[]> responseHeaders)
    {
        this.WorkflowName = workflowName;
        this.TriggerName = triggerName;
        this.callbackUrl = LogicAppUriRedaction.Redact(callbackUrl) ?? string.Empty;
        this.RunId = runId;
        this.StatusCode = statusCode;
        this.Status = status;
        this.ResponseBody = responseBody;
        this.ResponseHeaders = responseHeaders;
    }

    /// <summary>
    /// The resolved workflow name.
    /// </summary>
    public string WorkflowName { get; }

    /// <summary>
    /// The resolved trigger name.
    /// </summary>
    public string TriggerName { get; }


    /// <summary>
    /// The resolved workflow run identifier, when available.
    /// </summary>
    public string? RunId { get; }

    /// <summary>
    /// The HTTP status returned by the callback invocation.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// The inferred workflow outcome for the captured invocation.
    /// </summary>
    public LogicAppRunStatus Status { get; }

    /// <summary>
    /// The response body returned by the callback invocation.
    /// </summary>
    public string ResponseBody { get; }

    /// <summary>
    /// The response headers returned by the callback invocation.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> ResponseHeaders { get; }

    private readonly string callbackUrl;

    /// <summary>
    /// The callback URL that was invoked, with its shared-access signature redacted.
    /// </summary>
    public string CallbackUrl => callbackUrl;

    /// <summary>
    /// Gets the invoked callback URL using the consumer-facing invoke URI terminology.
    /// </summary>
    public string InvokeUri => CallbackUrl;

    /// <summary>
    /// Returns the normalized execution result for output binding and diagnostics.
    /// </summary>
    public LogicAppExecutionResult ExecutionResult => new(
        WorkflowName,
        TriggerName,
        CallbackUrl,
        RunId,
        StatusCode,
        Status,
        null,
        ResponseBody,
        ResponseHeaders,
        null,
        null);

    /// <summary>
    /// Creates an explicit run-tracking context for the captured Logic App invocation.
    /// </summary>
    internal LogicAppRunContext RunContext => new(
        WorkflowName,
        !string.IsNullOrWhiteSpace(RunId)
            ? RunId
            : throw new TestFramework.Core.Exceptions.FrameworkStateException("The Logic App capture result does not contain a workflow run id."));

    internal static IReadOnlyDictionary<string, string[]> EmptyHeaders { get; } = new ReadOnlyDictionary<string, string[]>(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));
}