namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Describes which Azure Logic App runtime model backs a configured Logic App identifier.
/// </summary>
public enum LogicAppHostingMode
{
    /// <summary>
    /// Azure Logic Apps Standard hosted on the Functions-based runtime.
    /// </summary>
    Standard,

    /// <summary>
    /// Azure Logic Apps Consumption using a user-supplied invoke URL and resource-id-backed management operations.
    /// </summary>
    Consumption,
}