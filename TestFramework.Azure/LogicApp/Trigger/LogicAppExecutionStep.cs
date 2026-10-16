using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Builder.LogicAppBuilder;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.LogicApp.Trigger;

internal sealed class LogicAppExecutionStep(
    LogicAppIdentifier identifier,
    LogicAppExecutionRequest request)
    : Step<LogicAppExecutionResult>, IHasEnvironmentRequirements
{
    public override string Name => request.TriggerMode == LogicAppTriggerExecutionMode.HttpCallback
        ? request.CaptureResponse
            ? "Http LogicApp Capture"
            : "Http LogicApp Execution"
        : "Management LogicApp Execution";

    public override string Description => request.TriggerMode == LogicAppTriggerExecutionMode.HttpCallback
        ? request.CaptureResponse
            ? "Triggers a Logic App manual HTTP workflow and captures the direct callback response."
            : "Triggers a Logic App manual HTTP workflow and returns the normalized execution context."
        : "Triggers a Logic App timer or recurrence workflow through the management endpoint.";

    public override bool DoesReturn => true;

    public override Step<LogicAppExecutionResult> Clone() => new LogicAppExecutionStep(identifier, request).WithClonedOptions(this);

    public override async Task<LogicAppExecutionResult?> Execute(RunContext context)
    {
        return request.TriggerMode switch
        {
            LogicAppTriggerExecutionMode.HttpCallback => await ExecuteHttpAsync(context, context.Deadline.Token),
            LogicAppTriggerExecutionMode.ManagementRun => await LogicAppManagementTrigger.ExecuteAsync(context, identifier, request.WorkflowName, request.TriggerName, context.Deadline.Token),
            _ => throw new TestFramework.Core.Exceptions.UnsupportedFrameworkValueException($"Unsupported Logic App trigger mode '{request.TriggerMode}'.")
        };
    }

    public override StepInstance<Step<LogicAppExecutionResult>, LogicAppExecutionResult> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
        contract.OptionalInput(request.WorkflowName);
        contract.OptionalInput(request.TriggerName);
        contract.Input(request.Request);
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureResourceKinds.LogicApp, identifier)];

    private async Task<LogicAppExecutionResult> ExecuteHttpAsync(RunContext context, CancellationToken cancellationToken)
    {
        VariableReference<CommonHttpRequest> httpRequest = request.Request
            ?? throw new TestFramework.Core.Exceptions.FrameworkStateException("HTTP Logic App execution requires a request payload builder.");

        LogicAppHttpInvokeResponse invocation = await LogicAppHttpTriggerExecution.InvokeAsync(
            context,
            identifier,
            request.WorkflowName,
            request.TriggerName,
            httpRequest,
            request.CaptureResponse,
            cancellationToken);

        return new LogicAppExecutionResult(
            invocation.WorkflowName,
            invocation.TriggerName,
            // Redacted by LogicAppExecutionResult itself - the SAS token must not reach results, logs,
            // reports or the DebugUI. The live invoke path uses its own local, unredacted copy.
            invocation.CallbackUrl,
            invocation.RunId,
            invocation.StatusCode,
            request.CaptureResponse ? LogicAppHttpTriggerExecution.ResolveCapturedStatus(invocation.StatusCode) : null,
            null,
            request.CaptureResponse ? invocation.ResponseBody : null,
            request.CaptureResponse ? invocation.ResponseHeaders : null,
            null,
            null);
    }
}

internal sealed record LogicAppExecutionRequest(
    VariableReference<string>? WorkflowName,
    VariableReference<string> TriggerName,
    LogicAppTriggerExecutionMode TriggerMode,
    VariableReference<CommonHttpRequest>? Request,
    bool CaptureResponse);

internal enum LogicAppTriggerExecutionMode
{
    HttpCallback,
    ManagementRun,
}