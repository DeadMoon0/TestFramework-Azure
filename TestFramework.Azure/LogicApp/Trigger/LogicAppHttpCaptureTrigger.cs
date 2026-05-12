using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.LogicApp.Trigger;

internal sealed class LogicAppHttpCaptureTrigger(
    LogicAppIdentifier identifier,
    VariableReference<string>? workflowName,
    VariableReference<string> triggerName,
    VariableReference<CommonHttpRequest> request)
    : Step<LogicAppCapturedResult>, IHasEnvironmentRequirements
{
    public override string Name => "Http LogicApp Capture Trigger";

    public override string Description => "Triggers a stateless Logic App manual HTTP workflow and captures the callback response directly.";

    public override bool DoesReturn => true;

    public override Step<LogicAppCapturedResult> Clone() => new LogicAppHttpCaptureTrigger(identifier, workflowName, triggerName, request).WithClonedOptions(this);

    public override async Task<LogicAppCapturedResult?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        LogicAppHttpInvokeResponse invocation = await LogicAppHttpTriggerExecution.InvokeAsync(serviceProvider, variableStore, identifier, workflowName, triggerName, request, cancellationToken);

        return new LogicAppCapturedResult(
            invocation.WorkflowName,
            invocation.TriggerName,
            invocation.CallbackUrl,
            invocation.RunId,
            invocation.StatusCode,
            LogicAppHttpTriggerExecution.ResolveCapturedStatus(invocation.StatusCode),
            invocation.ResponseBody,
            invocation.ResponseHeaders);
    }

    public override StepInstance<Step<LogicAppCapturedResult>, LogicAppCapturedResult> GetInstance() =>
        new StepInstance<Step<LogicAppCapturedResult>, LogicAppCapturedResult>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (workflowName is not null && workflowName.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(workflowName.Identifier!.Identifier, StepIOKind.Variable, false, typeof(string)));
        if (triggerName.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(triggerName.Identifier!.Identifier, StepIOKind.Variable, false, typeof(string)));
        if (request.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(request.Identifier!.Identifier, StepIOKind.Variable, true, typeof(CommonHttpRequest)));
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.LogicApp, identifier)];
}