namespace TestFramework.Azure;

/// <summary>
/// Defines how deep an Azure liveness check should go.
/// </summary>
public enum AlivenessLevel
{
    /// <summary>
    /// Verify only that the endpoint or account can be reached.
    /// </summary>
    Reachable,

    /// <summary>
    /// Verify that the configured credentials can authenticate against the resource.
    /// </summary>
    Authenticated,

    /// <summary>
    /// Verify the concrete configured resource, such as a container, table, queue, or function host path.
    /// </summary>
    Resource,
}