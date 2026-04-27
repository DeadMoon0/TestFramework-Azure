using System;
using System.Net.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Azure.FunctionApp;
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

    public IFunctionAppHttpPayloadStage SelectEndpoint(VariableReference<string> subPath, VariableReference<HttpMethod> method)
    {
        _routing = new ComposedRoutingVariable(subPath, method);
        return this;
    }

    public Step<HttpResponseMessage> Call()
    {
        if (_routing is null)
            throw new InvalidOperationException("No endpoint selected. Call SelectEndpoint or SelectEndpointWithMethod first.");

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

        public override TriggerHttpRouting? GetValue(VariableStore store) =>
            new TriggerHttpRouting(path.GetRequiredValue(store), method.GetRequiredValue(store), null!);

        public override VariableReference<TNew> Transform<TNew>(Func<TriggerHttpRouting?, TNew?> transform) where TNew : default =>
            throw new NotSupportedException();

        public override VariableReference<TNew> Transform<TNew>(Func<TriggerHttpRouting?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables) where TNew : default =>
            throw new NotSupportedException();
    }
}
