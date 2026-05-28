using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.LogicApp.Trigger;

internal static class LogicAppManagementTrigger
{
    internal static async Task<LogicAppExecutionResult> ExecuteAsync(IServiceProvider serviceProvider, VariableStore variableStore, LogicAppIdentifier identifier, VariableReference<string>? workflowName, VariableReference<string> triggerName, CancellationToken cancellationToken)
    {
        LogicAppConfig config = serviceProvider.GetRequiredService<ConfigStore<LogicAppConfig>>().GetConfig(identifier);
        config.EnsureSupportsManagement("trigger Logic App workflows through management endpoints");
        string resolvedWorkflowName = LogicAppRoutes.ResolveWorkflowName(config, workflowName?.GetValue(variableStore));
        string resolvedTriggerName = triggerName.GetRequiredValue(variableStore);
        IHttpRequestSender sender = serviceProvider.GetAzureComponentFactory().Http.CreateSender();
        DateTimeOffset observationStart = DateTimeOffset.UtcNow;

        Uri triggerRunUri = LogicAppRoutes.BuildTriggerRunUri(config, resolvedWorkflowName, resolvedTriggerName);
        using HttpRequestMessage request = LogicAppRoutes.CreateManagementRequest(HttpMethod.Post, triggerRunUri, config);
        await LogicAppRoutes.AuthorizeManagementRequestAsync(serviceProvider, identifier, config, request, cancellationToken);
        using HttpResponseMessage response = await sender.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict)
            {
                string observedRunId = await WaitForObservedRunAsync(serviceProvider, sender, identifier, config, resolvedWorkflowName, resolvedTriggerName, observationStart, cancellationToken);
                return new LogicAppExecutionResult(
                    resolvedWorkflowName,
                    resolvedTriggerName,
                    triggerRunUri.ToString(),
                    observedRunId,
                    HttpStatusCode.Accepted,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            response.EnsureSuccessStatusCode();
        }

        return new LogicAppExecutionResult(
            resolvedWorkflowName,
            resolvedTriggerName,
            triggerRunUri.ToString(),
            LogicAppRoutes.ResolveRunId(response),
            response.StatusCode,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    internal static async Task<string> WaitForObservedRunAsync(IServiceProvider serviceProvider, IHttpRequestSender sender, LogicAppIdentifier identifier, LogicAppConfig config, string workflowName, string triggerName, DateTimeOffset observationStart, CancellationToken cancellationToken)
    {
        Uri runsUri = LogicAppRoutes.BuildRunsUri(config, workflowName);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using HttpRequestMessage request = LogicAppRoutes.CreateManagementRequest(HttpMethod.Get, runsUri, config);
            await LogicAppRoutes.AuthorizeManagementRequestAsync(serviceProvider, identifier, config, request, cancellationToken);
            using HttpResponseMessage response = await sender.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                string payload = await response.Content.ReadAsStringAsync(cancellationToken);
                if (TryResolveObservedRunId(payload, triggerName, observationStart, out string? runId))
                    return runId!;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    internal static bool TryResolveObservedRunId(string payload, string triggerName, DateTimeOffset observationStart, out string? runId)
    {
        runId = null;

        using JsonDocument document = JsonDocument.Parse(payload);
        JsonElement runsRoot = document.RootElement;
        if (runsRoot.ValueKind == JsonValueKind.Object && runsRoot.TryGetProperty("value", out JsonElement valueElement))
            runsRoot = valueElement;

        if (runsRoot.ValueKind != JsonValueKind.Array)
            return false;

        foreach (JsonElement run in runsRoot.EnumerateArray().Reverse())
        {
            if (!TryGetRunTriggerName(run, out string? currentTriggerName)
                || !string.Equals(currentTriggerName, triggerName, StringComparison.OrdinalIgnoreCase)
                || !TryGetRunStartTime(run, out DateTimeOffset currentStartTime)
                || currentStartTime < observationStart)
            {
                continue;
            }

            runId = TryGetRunId(run);
            if (!string.IsNullOrWhiteSpace(runId))
                return true;
        }

        return false;
    }

    private static bool TryGetRunTriggerName(JsonElement run, out string? resolvedTriggerName)
    {
        resolvedTriggerName = null;
        if (!run.TryGetProperty("properties", out JsonElement propertiesElement)
            || !propertiesElement.TryGetProperty("trigger", out JsonElement triggerElement)
            || !triggerElement.TryGetProperty("name", out JsonElement nameElement))
        {
            return false;
        }

        resolvedTriggerName = nameElement.GetString();
        return !string.IsNullOrWhiteSpace(resolvedTriggerName);
    }

    private static bool TryGetRunStartTime(JsonElement run, out DateTimeOffset startTime)
    {
        startTime = DateTimeOffset.MinValue;
        if (!run.TryGetProperty("properties", out JsonElement propertiesElement))
            return false;

        if (propertiesElement.TryGetProperty("startTime", out JsonElement startTimeElement)
            && DateTimeOffset.TryParse(startTimeElement.GetString(), out startTime))
        {
            return true;
        }

        if (propertiesElement.TryGetProperty("trigger", out JsonElement triggerElement)
            && triggerElement.TryGetProperty("startTime", out JsonElement triggerStartTimeElement)
            && DateTimeOffset.TryParse(triggerStartTimeElement.GetString(), out startTime))
        {
            return true;
        }

        return false;
    }

    private static string? TryGetRunId(JsonElement run)
    {
        if (run.TryGetProperty("name", out JsonElement nameElement))
            return nameElement.GetString();

        if (run.TryGetProperty("id", out JsonElement idElement))
        {
            string? id = idElement.GetString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                string[] parts = id.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return parts.LastOrDefault();
            }
        }

        return null;
    }
}