using System;
using System.Net;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Result returned when a Logic App trigger is invoked.
/// </summary>
/// <param name="WorkflowName">The resolved workflow name.</param>
/// <param name="TriggerName">The resolved trigger name.</param>
/// <param name="CallbackUrl">The callback or management URL that was invoked.</param>
/// <param name="RunId">The resolved workflow run identifier, when available.</param>
/// <param name="StatusCode">The HTTP status returned by the trigger invocation.</param>
public sealed record LogicAppTriggerResult(
    string WorkflowName,
    string TriggerName,
    string CallbackUrl,
    string? RunId,
    HttpStatusCode StatusCode) : StepResultContext
{
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