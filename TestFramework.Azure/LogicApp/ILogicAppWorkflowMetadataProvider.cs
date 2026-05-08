using TestFramework.Azure.Identifier;

namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Provides optional workflow metadata for Logic App hosts that can describe workflow execution mode.
/// </summary>
public interface ILogicAppWorkflowMetadataProvider
{
    /// <summary>
    /// Attempts to resolve the workflow mode for the named workflow on the specified Logic App host.
    /// </summary>
    bool TryGetWorkflowMode(LogicAppIdentifier identifier, string workflowName, out LogicAppWorkflowMode mode);
}

/// <summary>
/// Describes whether a Logic App workflow persists run history or completes inline with the callback response.
/// </summary>
public enum LogicAppWorkflowMode
{
    /// <summary>
    /// The workflow mode could not be determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// The workflow persists run history and supports management run polling.
    /// </summary>
    Stateful,

    /// <summary>
    /// The workflow completes inline with the callback response and should use direct capture.
    /// </summary>
    Stateless,
}