using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Result returned when a stateless Logic App manual trigger is invoked and its callback response is captured directly.
/// </summary>
/// <param name="WorkflowName">The resolved workflow name.</param>
/// <param name="TriggerName">The resolved trigger name.</param>
/// <param name="CallbackUrl">The callback URL that was invoked.</param>
/// <param name="RunId">The resolved workflow run identifier, when available.</param>
/// <param name="StatusCode">The HTTP status returned by the callback invocation.</param>
/// <param name="Status">The inferred workflow outcome for the captured invocation.</param>
/// <param name="ResponseBody">The response body returned by the callback invocation.</param>
/// <param name="ResponseHeaders">The response headers returned by the callback invocation.</param>
public sealed record LogicAppCapturedResult(
    string WorkflowName,
    string TriggerName,
    string CallbackUrl,
    string? RunId,
    HttpStatusCode StatusCode,
    LogicAppRunStatus Status,
    string ResponseBody,
    IReadOnlyDictionary<string, string[]> ResponseHeaders) : StepResultContext
{
    /// <summary>
    /// Creates an explicit run-tracking context for the captured Logic App invocation.
    /// </summary>
    internal LogicAppRunContext RunContext => new(
        WorkflowName,
        !string.IsNullOrWhiteSpace(RunId)
            ? RunId
            : throw new InvalidOperationException("The Logic App capture result does not contain a workflow run id."));

    internal static IReadOnlyDictionary<string, string[]> EmptyHeaders { get; } = new ReadOnlyDictionary<string, string[]>(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));
}