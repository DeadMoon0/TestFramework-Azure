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
    /// Returns the current run context for typed result binding.
    /// </summary>
    internal LogicAppRunContext Context => this;
}