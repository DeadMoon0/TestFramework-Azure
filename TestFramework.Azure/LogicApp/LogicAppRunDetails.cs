namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Workflow run details returned by Logic App polling events.
/// </summary>
/// <param name="WorkflowName">The workflow name.</param>
/// <param name="RunId">The workflow run identifier.</param>
/// <param name="Status">The current workflow status.</param>
/// <param name="Code">The runtime code reported by the workflow, when available.</param>
/// <param name="OutputsJson">Serialized workflow outputs, when available.</param>
/// <param name="RawJson">The raw management payload used to build the result.</param>
public sealed record LogicAppRunDetails(
    string WorkflowName,
    string RunId,
    LogicAppRunStatus Status,
    string? Code,
    string? OutputsJson,
    string RawJson);