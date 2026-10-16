using System;
using System.Net.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Azure.FunctionApp.Trigger;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder;

internal sealed class RemoteFunctionAppBuilder(FunctionAppIdentifier appIdentifier) : IFunctionAppHttpConnectionStage
{
    public IFunctionAppHttpPayloadStage SelectEndpoint(VariableReference<string> subPath, VariableReference<HttpMethod> method) =>
        Payload(new ComposedRoutingVariable(subPath, method));

    public IFunctionAppHttpPayloadStage SelectFunction(string functionName, HttpMethod method)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        return SelectFunction(Var.Const(functionName), Var.Const(method));
    }

    public IFunctionAppHttpPayloadStage SelectFunction(VariableReference<string> functionName, VariableReference<HttpMethod> method) =>
        Payload(new DefaultFunctionRoutingVariable(functionName, method));

    // The payload stage exists only once an endpoint is chosen, so a call without one does not compile.
    private FunctionAppHttpPayloadBuilder Payload(VariableReference<TriggerHttpRouting> routing) =>
        new(request => new HttpRemoteFunctionAppTrigger(appIdentifier, routing, request));

    private sealed class ComposedRoutingVariable(VariableReference<string> path, VariableReference<HttpMethod> method)
        : VariableReference<TriggerHttpRouting>
    {
        public override bool RequireImmutability => false;
        public override bool HasIdentifier => false;
        public override VariableIdentifier? Identifier => null;

        public override TriggerHttpRouting? GetValue(VariableStore store)
        {
            string resolvedPath = path.GetRequiredValue(store);
            if (string.IsNullOrWhiteSpace(resolvedPath))
                throw new TestFramework.Core.Exceptions.FrameworkConfigurationException("Function App endpoint path cannot be blank.");

            return new TriggerHttpRouting(resolvedPath.Trim(), method.GetRequiredValue(store));
        }

        public override VariableReference<TNew> Transform<TNew>(Func<TriggerHttpRouting?, TNew?> transform) where TNew : default =>
            throw new NotSupportedException();

        public override VariableReference<TNew> Transform<TNew>(Func<TriggerHttpRouting?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables) where TNew : default =>
            throw new NotSupportedException();
    }

    private sealed class DefaultFunctionRoutingVariable(VariableReference<string> functionName, VariableReference<HttpMethod> method)
        : VariableReference<TriggerHttpRouting>
    {
        public override bool RequireImmutability => false;
        public override bool HasIdentifier => false;
        public override VariableIdentifier? Identifier => null;

        public override TriggerHttpRouting? GetValue(VariableStore store)
        {
            string resolvedFunctionName = functionName.GetRequiredValue(store);
            if (string.IsNullOrWhiteSpace(resolvedFunctionName))
                throw new TestFramework.Core.Exceptions.FrameworkConfigurationException("Function App function name cannot be blank.");

            // Relative to the host route prefix, which HttpRemoteFunctionAppTrigger applies from
            // FunctionAppConfig.RoutePrefix.
            string normalized = resolvedFunctionName.Trim().TrimStart('/');
            return new TriggerHttpRouting(normalized, method.GetRequiredValue(store));
        }

        public override VariableReference<TNew> Transform<TNew>(Func<TriggerHttpRouting?, TNew?> transform) where TNew : default =>
            throw new NotSupportedException();

        public override VariableReference<TNew> Transform<TNew>(Func<TriggerHttpRouting?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables) where TNew : default =>
            throw new NotSupportedException();
    }
}
