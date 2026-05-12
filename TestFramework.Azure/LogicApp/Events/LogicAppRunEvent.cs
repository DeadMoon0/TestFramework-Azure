using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.LogicApp;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Events;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.LogicApp.Events;

/// <summary>
/// Async event that polls a Logic App workflow run until it reaches the expected state.
/// </summary>
public sealed class LogicAppRunEvent(
    LogicAppIdentifier identifier,
    VariableReference<string>? workflowName,
    VariableReference<string> runId,
    VariableReference<LogicAppRunStatus>? expectedStatus,
    VariableReferenceGeneric? projectedSourceReference = null,
    Type? projectedSourceType = null)
    : AsyncEvent<LogicAppRunEvent, LogicAppRunDetails>, IHasEnvironmentRequirements
{
    /// <summary>
    /// Gets the display name used for this event.
    /// </summary>
    public override string Name => "LogicApp Run Event";

    /// <summary>
    /// Gets the event description.
    /// </summary>
    public override string Description => "Polls Logic App workflow management endpoints until a run reaches the requested state.";

    /// <summary>
    /// Gets a value indicating whether the event returns a value.
    /// </summary>
    public override bool DoesReturn => true;

    /// <summary>
    /// Creates a copy of this event and its options.
    /// </summary>
    /// <returns>The cloned event.</returns>
    public override Step<LogicAppRunDetails> Clone() => new LogicAppRunEvent(identifier, workflowName, runId, expectedStatus, projectedSourceReference, projectedSourceType).WithClonedOptions(this);

    /// <summary>
    /// Declares the event inputs required by the configured workflow and run references.
    /// </summary>
    /// <param name="contract">The I/O contract being populated.</param>
    public override void DeclareIO(StepIOContract contract)
    {
        if (projectedSourceReference is not null && projectedSourceReference.HasIdentifier)
        {
            contract.Inputs.Add(new StepIOEntry(
                projectedSourceReference.Identifier!.Identifier,
                StepIOKind.Variable,
                false,
                projectedSourceType ?? typeof(object)));
        }
        else
        {
            if (workflowName is not null && workflowName.HasIdentifier)
                contract.Inputs.Add(new StepIOEntry(workflowName.Identifier!.Identifier, StepIOKind.Variable, false, typeof(string)));
            if (runId.HasIdentifier)
                contract.Inputs.Add(new StepIOEntry(runId.Identifier!.Identifier, StepIOKind.Variable, false, typeof(string)));
        }

        if (expectedStatus is not null && expectedStatus.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(expectedStatus.Identifier!.Identifier, StepIOKind.Variable, false, typeof(LogicAppRunStatus)));
    }

    /// <summary>
    /// Declares the environment requirement for the configured Logic App host.
    /// </summary>
    /// <param name="variableStore">The variable store for the current run.</param>
    /// <returns>The required Azure environment resources.</returns>
    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.LogicApp, identifier)];

    /// <summary>
    /// Polls the Logic App management API for the targeted run.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve Azure services.</param>
    /// <param name="variableStore">The variable store used to resolve workflow and run identifiers.</param>
    /// <param name="artifactStore">The artifact store for the current run.</param>
    /// <param name="logger">The event logger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching run details, or <see langword="null"/> while polling should continue.</returns>
    public override async Task<LogicAppRunDetails?> DoEventPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        LogicAppConfig config = serviceProvider.GetRequiredService<ConfigStore<LogicAppConfig>>().GetConfig(identifier);
        string resolvedWorkflowName = LogicAppRoutes.ResolveWorkflowName(config, workflowName?.GetValue(variableStore));
        string resolvedRunId = runId.GetRequiredValue(variableStore);
        LogicAppRunStatus? expected = expectedStatus?.GetValue(variableStore);
        EnsureWorkflowModeSupportsRunPolling(serviceProvider, resolvedWorkflowName);
        IHttpRequestSender sender = serviceProvider.GetAzureComponentFactory().Http.CreateSender();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LogicAppRunDetails? details = await TryReadRunDetailsAsync(serviceProvider, sender, config, resolvedWorkflowName, resolvedRunId, cancellationToken);
            if (details is not null)
            {
                LogicAppRunDetails? resolved = ResolvePollingResult(details, expected);
                if (resolved is not null)
                    return resolved;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private static LogicAppRunDetails? ResolvePollingResult(LogicAppRunDetails details, LogicAppRunStatus? expected)
    {
        if (expected is null)
            return LogicAppJson.IsTerminal(details.Status) ? details : null;

        if (details.Status == expected.Value)
            return details;

        if (LogicAppJson.IsTerminal(details.Status))
            throw new InvalidOperationException($"Logic App run '{details.RunId}' for workflow '{details.WorkflowName}' reached terminal status '{details.Status}' instead of '{expected.Value}'.");

        return null;
    }

    private async Task<LogicAppRunDetails?> TryReadRunDetailsAsync(IServiceProvider serviceProvider, IHttpRequestSender sender, LogicAppConfig config, string workflowName, string runId, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = LogicAppRoutes.CreateManagementRequest(HttpMethod.Get, LogicAppRoutes.BuildRunUri(config, workflowName, runId), config);
        await LogicAppRoutes.AuthorizeManagementRequestAsync(serviceProvider, identifier, config, request, cancellationToken);
        using HttpResponseMessage response = await sender.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return await TryReadRunDetailsFromRunsListAsync(serviceProvider, sender, config, workflowName, runId, cancellationToken);

        response.EnsureSuccessStatusCode();
        string payload = await (response.Content?.ReadAsStringAsync(cancellationToken) ?? Task.FromResult(string.Empty));
        return LogicAppJson.ParseRunDetails(workflowName, runId, payload);
    }

    private async Task<LogicAppRunDetails?> TryReadRunDetailsFromRunsListAsync(IServiceProvider serviceProvider, IHttpRequestSender sender, LogicAppConfig config, string workflowName, string runId, CancellationToken cancellationToken)
    {
        using HttpRequestMessage listRequest = LogicAppRoutes.CreateManagementRequest(HttpMethod.Get, LogicAppRoutes.BuildRunsUri(config, workflowName), config);
        await LogicAppRoutes.AuthorizeManagementRequestAsync(serviceProvider, identifier, config, listRequest, cancellationToken);
        using HttpResponseMessage listResponse = await sender.SendAsync(listRequest, cancellationToken);
        if (!listResponse.IsSuccessStatusCode)
            return null;

        string listPayload = await (listResponse.Content?.ReadAsStringAsync(cancellationToken) ?? Task.FromResult(string.Empty));
        return LogicAppJson.TryParseRunDetailsFromRunsList(workflowName, runId, listPayload);
    }

    private void EnsureWorkflowModeSupportsRunPolling(IServiceProvider serviceProvider, string resolvedWorkflowName)
    {
        ILogicAppWorkflowMetadataProvider? metadataProvider = serviceProvider.GetService<ILogicAppWorkflowMetadataProvider>();
        if (metadataProvider is null)
            return;

        if (!metadataProvider.TryGetWorkflowMode(identifier, resolvedWorkflowName, out LogicAppWorkflowMode mode))
            return;

        if (mode == LogicAppWorkflowMode.Stateless)
        {
            throw new InvalidOperationException(
                $"Logic App workflow '{resolvedWorkflowName}' on '{identifier}' is stateless and does not support managed run polling through RunCompleted/RunReachedStatus. "
                + "Use CallAndCapture() for stateless workflows instead.");
        }
    }
}