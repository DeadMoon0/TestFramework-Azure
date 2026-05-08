namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Canonical workflow run states used by the Logic App test surface.
/// </summary>
public enum LogicAppRunStatus
{
    /// <summary>
    /// The runtime did not report a recognized status value.
    /// </summary>
    Unknown,

    /// <summary>
    /// The workflow run is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// The workflow run is waiting on an external dependency or condition.
    /// </summary>
    Waiting,

    /// <summary>
    /// The workflow run finished successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The workflow run finished with a failure.
    /// </summary>
    Failed,

    /// <summary>
    /// The workflow run was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The workflow run exceeded its runtime timeout.
    /// </summary>
    TimedOut,

    /// <summary>
    /// The workflow run aborted before normal completion.
    /// </summary>
    Aborted,

    /// <summary>
    /// The workflow run was skipped.
    /// </summary>
    Skipped,
}