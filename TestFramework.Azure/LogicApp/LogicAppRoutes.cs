using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;

namespace TestFramework.Azure.LogicApp;

internal static class LogicAppRoutes
{
    internal const string StandardApiVersion = "2022-03-01";
    internal const string ConsumptionManagementApiVersion = "2019-05-01";

    internal static Uri BuildListCallbackUrlUri(LogicAppConfig config, string workflowName, string triggerName)
    {
        if (config.HostingMode == LogicAppHostingMode.Consumption)
            return ResolveConsumptionUri(config.Consumption.InvokeUrl, workflowName, triggerName, null, $"{nameof(LogicAppConfig.Consumption)}:{nameof(LogicAppConsumptionConfig.InvokeUrl)}");

        string workflow = Uri.EscapeDataString(workflowName);
        string trigger = Uri.EscapeDataString(triggerName);
        return new Uri(new Uri(RequireStandardBaseUrl(config)), $"runtime/webhooks/workflow/api/management/workflows/{workflow}/triggers/{trigger}/listCallbackUrl?api-version={StandardApiVersion}");
    }

    internal static Uri BuildRunUri(LogicAppConfig config, string workflowName, string runId)
    {
        if (config.HostingMode == LogicAppHostingMode.Consumption)
            return BuildConsumptionManagementUri(config, $"runs/{Uri.EscapeDataString(runId)}");

        string workflow = Uri.EscapeDataString(workflowName);
        string run = Uri.EscapeDataString(runId);
        return new Uri(new Uri(RequireStandardBaseUrl(config)), $"runtime/webhooks/workflow/api/management/workflows/{workflow}/runs/{run}?api-version={StandardApiVersion}");
    }

    internal static Uri BuildTriggerRunUri(LogicAppConfig config, string workflowName, string triggerName)
    {
        if (config.HostingMode == LogicAppHostingMode.Consumption)
            return BuildConsumptionManagementUri(config, $"triggers/{Uri.EscapeDataString(triggerName)}/run");

        string workflow = Uri.EscapeDataString(workflowName);
        string trigger = Uri.EscapeDataString(triggerName);
        return new Uri(new Uri(RequireStandardBaseUrl(config)), $"runtime/webhooks/workflow/api/management/workflows/{workflow}/triggers/{trigger}/run?api-version={StandardApiVersion}");
    }

    internal static Uri BuildRunsUri(LogicAppConfig config, string workflowName)
    {
        if (config.HostingMode == LogicAppHostingMode.Consumption)
            return BuildConsumptionManagementUri(config, "runs");

        string workflow = Uri.EscapeDataString(workflowName);
        return new Uri(new Uri(RequireStandardBaseUrl(config)), $"runtime/webhooks/workflow/api/management/workflows/{workflow}/runs?api-version={StandardApiVersion}");
    }

    internal static HttpRequestMessage CreateManagementRequest(HttpMethod method, Uri uri, LogicAppConfig config)
    {
        HttpRequestMessage request = new(method, uri);
        if (config.HostingMode == LogicAppHostingMode.Consumption)
            return request;

        string? hostKey = config.Standard.AdminCode ?? config.Standard.Code;
        if (!string.IsNullOrWhiteSpace(hostKey))
            request.Headers.Add("x-functions-key", hostKey);
        return request;
    }

    internal static async Task AuthorizeManagementRequestAsync(IServiceProvider serviceProvider, LogicAppIdentifier identifier, LogicAppConfig config, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (config.HostingMode == LogicAppHostingMode.Standard)
            return;

        ILogicAppConsumptionManagementRequestAuthorizer authorizer = serviceProvider.GetService<ILogicAppConsumptionManagementRequestAuthorizer>()
            ?? throw new InvalidOperationException(
                $"Logic App '{identifier}' requires management capability for this operation. "
                + $"Register {nameof(ILogicAppConsumptionManagementRequestAuthorizer)} to enable RunCompleted(...), RunReachedStatus(...), timer/recurrence triggers, and management-backed liveness for Consumption workflows.");
        await authorizer.AuthorizeAsync(identifier, config, request, cancellationToken);
    }

    internal static string ResolveWorkflowName(LogicAppConfig config, string? workflowName)
    {
        return !string.IsNullOrWhiteSpace(workflowName)
            ? workflowName
            : config.WorkflowName ?? throw new InvalidOperationException("No Logic App workflow name was provided, and LogicAppConfig.WorkflowName is not configured.");
    }

    internal static string? ResolveRunId(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("x-ms-workflow-run-id", out IEnumerable<string>? runIdValues))
            return runIdValues.FirstOrDefault();

        string? location = response.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(location))
            return null;

        string[] parts = location.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int runIndex = Array.FindLastIndex(parts, part => string.Equals(part, "runs", StringComparison.OrdinalIgnoreCase));
        if (runIndex >= 0 && runIndex + 1 < parts.Length)
            return parts[runIndex + 1].Split('?', 2)[0];

        return null;
    }

    internal static Uri BuildIsLiveProbeUri(LogicAppConfig config, string? workflowName)
    {
        if (config.HostingMode == LogicAppHostingMode.Standard)
            return new Uri(new Uri(RequireStandardBaseUrl(config)), "admin/host/status");

        string resolvedWorkflowName = ResolveWorkflowName(config, workflowName);
        if (HasConsumptionManagementResourceId(config))
            return BuildRunsUri(config, resolvedWorkflowName);

        if (!string.IsNullOrWhiteSpace(config.Consumption.InvokeUrl))
            return ResolveConsumptionUri(config.Consumption.InvokeUrl, resolvedWorkflowName, null, null, $"{nameof(LogicAppConfig.Consumption)}:{nameof(LogicAppConsumptionConfig.InvokeUrl)}");

        throw new InvalidOperationException(
            $"Logic App '{resolvedWorkflowName}' in Consumption mode has no usable liveness path configured. "
            + $"Set '{nameof(LogicAppConfig.Consumption)}:{nameof(LogicAppConsumptionConfig.InvokeUrl)}' for invoke-only access, or '{nameof(LogicAppConfig.Consumption)}:{nameof(LogicAppConsumptionConfig.WorkflowResourceId)}' for management-backed liveness.");
    }

    internal static bool HasConsumptionManagementResourceId(LogicAppConfig config)
        => !string.IsNullOrWhiteSpace(config.Consumption.WorkflowResourceId);

    internal static string RequireStandardBaseUrl(LogicAppConfig config)
        => config.Standard.BaseUrl ?? throw new InvalidOperationException($"Logic App in Standard mode requires '{nameof(LogicAppConfig.Standard)}:{nameof(LogicAppStandardConfig.BaseUrl)}' to be configured.");

    private static Uri BuildConsumptionManagementUri(LogicAppConfig config, string relativePath)
    {
        string resourceId = config.Consumption.WorkflowResourceId
            ?? throw new InvalidOperationException(
                $"Logic App Consumption management operations require '{nameof(LogicAppConfig.Consumption)}:{nameof(LogicAppConsumptionConfig.WorkflowResourceId)}'. "
                + "Invoke-only scenarios can omit it, but RunCompleted(...), RunReachedStatus(...), timer/recurrence triggers, and management-backed liveness cannot.");
        string trimmedResourceId = resourceId.TrimEnd('/');
        return new Uri($"https://management.azure.com{trimmedResourceId}/{relativePath}?api-version={ConsumptionManagementApiVersion}", UriKind.Absolute);
    }

    private static Uri ResolveConsumptionUri(string? template, string workflowName, string? triggerName, string? runId, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(template))
            throw new InvalidOperationException(
                $"Logic App Consumption invoke operations require '{propertyName}'. "
                + "Configure it for Manual().Call(), CallAndCapture(), and invoke-only liveness.");

        string resolved = template
            .Replace("{workflowName}", Uri.EscapeDataString(workflowName), StringComparison.Ordinal)
            .Replace("{triggerName}", Uri.EscapeDataString(triggerName ?? string.Empty), StringComparison.Ordinal)
            .Replace("{runId}", Uri.EscapeDataString(runId ?? string.Empty), StringComparison.Ordinal);

        return new Uri(resolved, UriKind.Absolute);
    }
}