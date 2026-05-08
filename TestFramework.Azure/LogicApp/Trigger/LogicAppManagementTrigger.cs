using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.LogicApp.Trigger;

internal sealed class LogicAppManagementTrigger(
    LogicAppIdentifier identifier,
    VariableReference<string>? workflowName,
    VariableReference<string> triggerName)
    : Step<LogicAppTriggerResult>, IHasEnvironmentRequirements
{
    public override string Name => "Management LogicApp Trigger";

    public override string Description => "Triggers a Logic App timer or recurrence workflow through the management trigger run endpoint.";

    public override bool DoesReturn => true;

    public override Step<LogicAppTriggerResult> Clone() => new LogicAppManagementTrigger(identifier, workflowName, triggerName).WithClonedOptions(this);

    public override async Task<LogicAppTriggerResult?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        LogicAppConfig config = serviceProvider.GetRequiredService<ConfigStore<LogicAppConfig>>().GetConfig(identifier);
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
                string observedRunId = await WaitForScheduledRunAsync(serviceProvider, sender, identifier, config, resolvedWorkflowName, resolvedTriggerName, observationStart, cancellationToken);
                return new LogicAppTriggerResult(
                    resolvedWorkflowName,
                    resolvedTriggerName,
                    triggerRunUri.ToString(),
                    observedRunId,
                    HttpStatusCode.Accepted);
            }

            response.EnsureSuccessStatusCode();
        }

        return new LogicAppTriggerResult(
            resolvedWorkflowName,
            resolvedTriggerName,
            triggerRunUri.ToString(),
            LogicAppRoutes.ResolveRunId(response),
            response.StatusCode);
    }

    private static async Task<string> WaitForScheduledRunAsync(IServiceProvider serviceProvider, IHttpRequestSender sender, LogicAppIdentifier identifier, LogicAppConfig config, string workflowName, string triggerName, DateTimeOffset observationStart, CancellationToken cancellationToken)
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

    private static bool TryResolveObservedRunId(string payload, string triggerName, DateTimeOffset observationStart, out string? runId)
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

    public override StepInstance<Step<LogicAppTriggerResult>, LogicAppTriggerResult> GetInstance() =>
        new StepInstance<Step<LogicAppTriggerResult>, LogicAppTriggerResult>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (workflowName is not null && workflowName.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(workflowName.Identifier!.Identifier, StepIOKind.Variable, false, typeof(string)));
        if (triggerName.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(triggerName.Identifier!.Identifier, StepIOKind.Variable, false, typeof(string)));
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.LogicApp, identifier)];
}