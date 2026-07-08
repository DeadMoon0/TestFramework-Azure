using System;
using TestFramework.Azure.LogicApp;

namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// Configuration required to call and manage an Azure Logic App host.
/// </summary>
/// <remarks>
/// The identifier maps to a named entry under the <c>LogicApp</c> section.
/// Standard and Consumption settings are modeled separately so callers only provide values for the hosting model they use.
/// </remarks>
public record LogicAppConfig
{
    /// <summary>
    /// Selects whether the configured Logic App uses the Standard host model or the Consumption callback-url model.
    /// </summary>
    public LogicAppHostingMode HostingMode { get; init; } = LogicAppHostingMode.Standard;

    /// <summary>
    /// Optional default workflow name used by higher-level helpers.
    /// </summary>
    public string? WorkflowName { get; init; }

    /// <summary>
    /// Settings used when the Logic App is hosted on the Standard runtime.
    /// </summary>
    public LogicAppStandardConfig Standard { get; init; } = new();

    /// <summary>
    /// Settings used when the Logic App is hosted on the Consumption runtime.
    /// </summary>
    public LogicAppConsumptionConfig Consumption { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether this Logic App can be invoked directly.
    /// </summary>
    public bool SupportsDirectInvocation => HostingMode switch
    {
        LogicAppHostingMode.Standard => !string.IsNullOrWhiteSpace(Standard.BaseUrl),
        LogicAppHostingMode.Consumption => !string.IsNullOrWhiteSpace(Consumption.InvokeUrl),
        _ => false,
    };

    /// <summary>
    /// Gets a value indicating whether this Logic App supports management operations.
    /// </summary>
    public bool SupportsManagementOperations => HostingMode switch
    {
        LogicAppHostingMode.Standard => !string.IsNullOrWhiteSpace(Standard.BaseUrl),
        LogicAppHostingMode.Consumption => !string.IsNullOrWhiteSpace(Consumption.WorkflowResourceId),
        _ => false,
    };

    internal void EnsureSupportsDirectInvocation(string operationDescription)
    {
        if (SupportsDirectInvocation)
            return;

        throw HostingMode switch
        {
            LogicAppHostingMode.Standard => new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"Logic App direct invocation requires '{nameof(Standard)}:{nameof(LogicAppStandardConfig.BaseUrl)}' to {operationDescription}."),
            LogicAppHostingMode.Consumption => new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"Logic App direct invocation requires '{nameof(Consumption)}:{nameof(LogicAppConsumptionConfig.InvokeUrl)}' to {operationDescription}."),
            _ => new TestFramework.Core.Exceptions.FrameworkConfigurationException($"Logic App direct invocation is not configured to {operationDescription}."),
        };
    }

    internal void EnsureSupportsManagement(string operationDescription)
    {
        if (SupportsManagementOperations)
            return;

        throw HostingMode switch
        {
            LogicAppHostingMode.Standard => new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"Logic App management operations require '{nameof(Standard)}:{nameof(LogicAppStandardConfig.BaseUrl)}' to {operationDescription}."),
            LogicAppHostingMode.Consumption => new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"Logic App management operations require '{nameof(Consumption)}:{nameof(LogicAppConsumptionConfig.WorkflowResourceId)}' to {operationDescription}."),
            _ => new TestFramework.Core.Exceptions.FrameworkConfigurationException($"Logic App management operations are not configured to {operationDescription}."),
        };
    }
}

/// <summary>
/// Standard-hosted Logic App connection settings.
/// </summary>
public record LogicAppStandardConfig
{
    /// <summary>
    /// Absolute Logic App host URL, for example <c>https://my-logic.azurewebsites.net/</c>.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Optional runtime key used for Standard invocation helpers.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Optional host-level admin key used for Standard management endpoints.
    /// </summary>
    public string? AdminCode { get; init; }
}

/// <summary>
/// Consumption-hosted Logic App connection settings.
/// </summary>
public record LogicAppConsumptionConfig
{
    /// <summary>
    /// Direct invoke URL for request-trigger execution.
    /// Supports <c>{workflowName}</c> and <c>{triggerName}</c> tokens.
    /// </summary>
    public string? InvokeUrl { get; init; }

    /// <summary>
    /// Optional Azure resource ID used for authenticated management operations in Consumption mode.
    /// Example: <c>/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Logic/workflows/{workflowName}</c>
    /// </summary>
    public string? WorkflowResourceId { get; init; }
}