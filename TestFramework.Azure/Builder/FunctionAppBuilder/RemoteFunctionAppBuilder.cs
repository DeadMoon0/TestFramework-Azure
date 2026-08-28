using System;
using System.Collections.Generic;
using System.Net.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Azure.FunctionApp;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Azure.FunctionApp.Trigger;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder;

internal class RemoteFunctionAppBuilder(FunctionAppIdentifier appIdentifier)
    : IFunctionAppHttpConnectionStage, IFunctionAppHttpPayloadStage
{
    private VariableReference<TriggerHttpRouting>? _routing;
    private readonly FunctionAppHttpRequestBuilderState _request = new();

    public IFunctionAppHttpPayloadStage SelectEndpointWithMethod<TFunctionType>(string methodName)
    {
        _routing = FunctionAppMethodAnalyzer.GetHttpTriggerRouting<TFunctionType>(methodName);
        return this;
    }

    public IFunctionAppHttpPayloadStage SelectEndpointWithMethod<TFunctionType>(string methodName, HttpMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);
        _routing = FunctionAppMethodAnalyzer.GetHttpTriggerRouting<TFunctionType>(methodName, method);
        return this;
    }

    public IFunctionAppHttpPayloadStage SelectEndpoint(VariableReference<string> subPath, VariableReference<HttpMethod> method)
    {
        _routing = new ComposedRoutingVariable(subPath, method);
        return this;
    }

    public IFunctionAppHttpPayloadStage SelectFunction(string functionName, HttpMethod method)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        return SelectFunction(Var.Const(functionName), Var.Const(method));
    }

    public IFunctionAppHttpPayloadStage SelectFunction(VariableReference<string> functionName, VariableReference<HttpMethod> method)
    {
        _routing = new DefaultFunctionRoutingVariable(functionName, method);
        return this;
    }

    public Step<HttpResponseResultContext> Call()
    {
        if (_routing is null)
            throw new TestFramework.Core.Exceptions.FrameworkStateException($"Function App '{appIdentifier}' has no selected HTTP endpoint. Call SelectFunction(...), SelectEndpoint(...), or SelectEndpointWithMethod(...) first.");

        return new HttpRemoteFunctionAppTrigger(appIdentifier, _routing, _request.BuildVariable());
    }

    public IFunctionAppHttpPayloadStage WithBody(VariableReference<string> text)
    {
        _request.SetBody(text);
        return this;
    }

    public IFunctionAppHttpPayloadStage WithBody(VariableReference<byte[]> data)
    {
        _request.SetBody(data);
        return this;
    }

    public IFunctionAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string> value)
    {
        _request.AddHeader(key, value);
        return this;
    }

    public IFunctionAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string[]> values)
    {
        _request.AddHeader(key, values);
        return this;
    }

    public IFunctionAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string>> headers)
    {
        _request.AddHeaders(headers);
        return this;
    }

    public IFunctionAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string[]>> headers)
    {
        _request.AddHeaders(headers);
        return this;
    }

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

            return new TriggerHttpRouting(resolvedPath.Trim(), method.GetRequiredValue(store), null!);
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
            return new TriggerHttpRouting(normalized, method.GetRequiredValue(store), null!);
        }

        public override VariableReference<TNew> Transform<TNew>(Func<TriggerHttpRouting?, TNew?> transform) where TNew : default =>
            throw new NotSupportedException();

        public override VariableReference<TNew> Transform<TNew>(Func<TriggerHttpRouting?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables) where TNew : default =>
            throw new NotSupportedException();
    }
}
