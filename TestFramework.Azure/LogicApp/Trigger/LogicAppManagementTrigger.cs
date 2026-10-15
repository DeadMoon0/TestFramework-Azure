using TestFramework.Config;
using TestFramework.Core.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.LogicApp.Trigger;

internal static class LogicAppManagementTrigger
{
    /// <remarks>
    /// Takes the run rather than two pieces of one, for the same reason the HTTP path does.
    /// </remarks>
    internal static async Task<LogicAppExecutionResult> ExecuteAsync(RunContext context, LogicAppIdentifier identifier, VariableReference<string>? workflowName, VariableReference<string> triggerName, CancellationToken cancellationToken)
    {
        VariableStore variableStore = context.Variables;
        IServiceProvider serviceProvider = context.Services;

        LogicAppConfig config = context.Configured<LogicAppConfig>(identifier);
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

            await Task.Delay(AzureTimings.LogicAppPollInterval, cancellationToken);
        }
    }

    internal static bool TryResolveObservedRunId(string payload, string triggerName, DateTimeOffset observationStart, out string? runId)
    {
        runId = null;

        // Through the engine's parse: this payload is Azure's, and a startTime read as a string has to
        // still be a string when it is read. JToken.Parse would have turned it into a date.
        JToken runsRoot = WireJson.Parse(payload);
        if (runsRoot is JObject envelope && envelope["value"] is { } valueElement)
            runsRoot = valueElement;

        if (runsRoot is not JArray runs)
            return false;

        foreach (JToken run in runs.Reverse())
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

    private static bool TryGetRunTriggerName(JToken run, out string? resolvedTriggerName)
    {
        resolvedTriggerName = run["properties"]?["trigger"]?["name"]?.Value<string>();

        return !string.IsNullOrWhiteSpace(resolvedTriggerName);
    }

    private static bool TryGetRunStartTime(JToken run, out DateTimeOffset startTime)
    {
        startTime = DateTimeOffset.MinValue;

        JToken? properties = run["properties"];
        if (properties is null)
            return false;

        // The run's own start time, else the trigger's - a run that has not started reports only the
        // latter.
        return DateTimeOffset.TryParse(properties["startTime"]?.Value<string>(), out startTime)
            || DateTimeOffset.TryParse(properties["trigger"]?["startTime"]?.Value<string>(), out startTime);
    }

    private static string? TryGetRunId(JToken run)
    {
        if (run["name"]?.Value<string>() is { } name)
            return name;

        // An id is the full resource path; the run is its last segment.
        if (run["id"]?.Value<string>() is { } id && !string.IsNullOrWhiteSpace(id))
        {
            string[] parts = id.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return parts.LastOrDefault();
        }

        return null;
    }
}