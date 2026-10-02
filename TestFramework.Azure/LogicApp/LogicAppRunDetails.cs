using TestFramework.Core.Steps;

namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Workflow run details returned by Logic App polling events.
/// </summary>
public sealed record LogicAppRunDetails : StepResultContext
{
    internal LogicAppRunDetails(string workflowName, string runId, LogicAppRunStatus status, string? code, string? outputsJson, string rawJson)
    {
        this.WorkflowName = workflowName;
        this.RunId = runId;
        this.Status = status;
        this.Code = code;
        this.OutputsJson = outputsJson;
        this.RawJson = rawJson;
    }

    /// <summary>
    /// The workflow name.
    /// </summary>
    public string WorkflowName { get; }

    /// <summary>
    /// The workflow run identifier.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// The current workflow status.
    /// </summary>
    public LogicAppRunStatus Status { get; }

    /// <summary>
    /// The runtime code reported by the workflow, when available.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// Serialized workflow outputs, when available.
    /// </summary>
    public string? OutputsJson { get; }

    /// <summary>
    /// The raw management payload used to build the result.
    /// </summary>
    public string RawJson { get; }

    /// <summary>
    /// Returns the normalized execution result for output binding and diagnostics.
    /// </summary>
    public LogicAppExecutionResult ExecutionResult => new(
        WorkflowName,
        null,
        null,
        RunId,
        null,
        Status,
        Code,
        null,
        null,
        OutputsJson,
        RawJson);
};