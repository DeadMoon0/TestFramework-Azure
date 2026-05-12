using System;
using System.Net;

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
    HttpStatusCode StatusCode)
{
    /// <summary>
    /// Creates an explicit run-tracking context for stateful Logic App polling.
    /// </summary>
    public LogicAppRunContext RunContext => new(
        WorkflowName,
        !string.IsNullOrWhiteSpace(RunId)
            ? RunId
            : throw new InvalidOperationException("The Logic App trigger result does not contain a workflow run id."));
}