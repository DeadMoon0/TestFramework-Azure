using System.Text;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Explicit context that identifies a stateful Logic App workflow run.
/// </summary>
public sealed record LogicAppRunContext : StepResultContext
{
    internal LogicAppRunContext(string workflowName, string runId)
    {
        this.WorkflowName = workflowName;
        this.RunId = runId;
    }

    /// <summary>
    /// The resolved workflow name.
    /// </summary>
    public string WorkflowName { get; }

    /// <summary>
    /// The resolved workflow run identifier.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Returns the lightweight execution summary that can be reconstructed from workflow and run identifiers alone.
    /// </summary>
    public LogicAppExecutionResult RunSummary => ExecutionResult;

    /// <summary>
    /// Returns the normalized execution result for output binding and diagnostics.
    /// </summary>
    public LogicAppExecutionResult ExecutionResult => new(
        WorkflowName,
        null,
        null,
        RunId,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    /// <summary>
    /// Returns the current run context for typed result binding.
    /// </summary>
    internal LogicAppRunContext Context => this;

    /// <summary>
    /// Formats only the run identifier fields and avoids traversing derived execution results.
    /// </summary>
    protected override bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"WorkflowName = {WorkflowName}");
        builder.Append($", RunId = {RunId}");
        return true;
    }
}