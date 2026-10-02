using System;
using System.Net;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Result returned when a Logic App trigger is invoked.
/// </summary>
public sealed record LogicAppTriggerResult : StepResultContext
{
    internal LogicAppTriggerResult(string workflowName, string triggerName, string callbackUrl, string? runId, HttpStatusCode statusCode)
    {
        this.WorkflowName = workflowName;
        this.TriggerName = triggerName;
        this.CallbackUrl = LogicAppUriRedaction.Redact(callbackUrl)!;
        this.RunId = runId;
        this.StatusCode = statusCode;
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
    /// The HTTP status returned by the trigger invocation.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// The callback or management URL that was invoked, with its signature redacted.
    /// </summary>
    /// <remarks>
    /// Redacted by construction like the two sibling result records: a Consumption callback URL's
    /// <c>sig</c> is a bearer credential, and a result travels into variables, logs and the frozen
    /// run. This was the one result type that kept the raw URL.
    /// </remarks>
    public string CallbackUrl { get; }

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
        null,
        null,
        null,
        null,
        null,
        null);

    /// <summary>
    /// Creates an explicit run-tracking context for stateful Logic App polling.
    /// </summary>
    public LogicAppRunContext RunContext => new(
        WorkflowName,
        !string.IsNullOrWhiteSpace(RunId)
            ? RunId
            : throw new TestFramework.Core.Exceptions.FrameworkStateException("The Logic App trigger result does not contain a workflow run id."));
}