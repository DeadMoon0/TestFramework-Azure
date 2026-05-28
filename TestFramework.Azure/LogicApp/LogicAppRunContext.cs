using System.Text;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Explicit context that identifies a stateful Logic App workflow run.
/// </summary>
/// <param name="WorkflowName">The resolved workflow name.</param>
/// <param name="RunId">The resolved workflow run identifier.</param>
public sealed record LogicAppRunContext(
    string WorkflowName,
    string RunId) : StepResultContext
{
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