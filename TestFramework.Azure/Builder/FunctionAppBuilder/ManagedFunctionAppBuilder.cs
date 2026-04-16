using System;
using TestFramework.Azure.FunctionApp;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Azure.FunctionApp.Trigger;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder;

internal sealed class ManagedFunctionAppBuilder(FunctionAppIdentifier appIdentifier)
{
    private VariableReference<TriggerRouting>? _routing;

    public ManagedFunctionAppBuilder SelectFunctionWithMethod<TFunctionType>(string methodName)
    {
        _routing = Var.Const(FunctionAppMethodAnalyzer.GetTriggerRouting<TFunctionType>(methodName));
        return this;
    }

    public Step<ManagedResult> Call()
    {
        if (_routing is null)
            throw new InvalidOperationException("No function selected. Call SelectFunctionWithMethod first.");

        return new ManagedRemoteFunctionAppTrigger(appIdentifier, _routing);
    }
}