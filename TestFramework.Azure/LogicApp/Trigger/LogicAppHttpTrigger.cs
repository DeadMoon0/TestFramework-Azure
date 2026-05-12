using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.LogicApp.Trigger;

internal sealed class LogicAppHttpTrigger(
    LogicAppIdentifier identifier,
    VariableReference<string>? workflowName,
    VariableReference<string> triggerName,
    VariableReference<CommonHttpRequest> request)
    : Step<LogicAppTriggerResult>, IHasEnvironmentRequirements
{
    public override string Name => "Http LogicApp Trigger";

    public override string Description => "Triggers a Logic App manual HTTP workflow by resolving and invoking its callback URL.";

    public override bool DoesReturn => true;

    public override Step<LogicAppTriggerResult> Clone() => new LogicAppHttpTrigger(identifier, workflowName, triggerName, request).WithClonedOptions(this);

    public override async Task<LogicAppTriggerResult?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        LogicAppHttpInvokeResponse invocation = await LogicAppHttpTriggerExecution.InvokeAsync(serviceProvider, variableStore, identifier, workflowName, triggerName, request, cancellationToken);

        return new LogicAppTriggerResult(
            invocation.WorkflowName,
            invocation.TriggerName,
            invocation.CallbackUrl,
            invocation.RunId,
            invocation.StatusCode);
    }

    public override StepInstance<Step<LogicAppTriggerResult>, LogicAppTriggerResult> GetInstance() =>
        new StepInstance<Step<LogicAppTriggerResult>, LogicAppTriggerResult>(this);

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