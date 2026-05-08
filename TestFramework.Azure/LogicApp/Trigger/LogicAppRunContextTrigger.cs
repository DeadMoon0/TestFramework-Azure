using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.LogicApp.Trigger;

internal sealed class LogicAppRunContextTrigger(Step<LogicAppTriggerResult> inner)
    : Step<LogicAppRunContext>, IHasEnvironmentRequirements
{
    public override string Name => inner.Name + " Run Context";

    public override string Description => inner.Description + " Returns the explicit workflow/run identity for stateful polling.";

    public override bool DoesReturn => true;

    public override Step<LogicAppRunContext> Clone() => new LogicAppRunContextTrigger(inner.Clone()).WithClonedOptions(this);

    public override async Task<LogicAppRunContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        LogicAppTriggerResult? result = await inner.Execute(serviceProvider, variableStore, artifactStore, logger, cancellationToken);
        return result?.RunContext;
    }

    public override StepInstance<Step<LogicAppRunContext>, LogicAppRunContext> GetInstance() =>
        new StepInstance<Step<LogicAppRunContext>, LogicAppRunContext>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        inner.DeclareIO(contract);
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => inner is IHasEnvironmentRequirements requirements
            ? requirements.GetEnvironmentRequirements(variableStore)
            : [];
}