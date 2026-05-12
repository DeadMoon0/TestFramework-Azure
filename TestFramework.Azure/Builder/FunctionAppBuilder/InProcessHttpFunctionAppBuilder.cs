using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Azure.FunctionApp.InProcessProxies;
using TestFramework.Azure.FunctionApp.Trigger;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder;

internal class InProcessHttpFunctionAppBuilder<TFunction> : IFunctionAppHttpPayloadStage where TFunction : notnull
{
    private readonly Func<TFunction, FunctionAppHttpInProcessCallProxy, Task<IActionResult?>> _action;
    private readonly FunctionAppHttpRequestBuilderState _request = new();

    internal InProcessHttpFunctionAppBuilder(Action<TFunction, FunctionAppHttpInProcessCallProxy> action)
    {
        _action = (f, p) => { action(f, p); return Task.FromResult<IActionResult?>(null); };
    }

    internal InProcessHttpFunctionAppBuilder(Func<TFunction, FunctionAppHttpInProcessCallProxy, Task> action)
    {
        _action = async (f, p) => { await action(f, p); return null; };
    }

    internal InProcessHttpFunctionAppBuilder(Func<TFunction, FunctionAppHttpInProcessCallProxy, IActionResult> func)
    {
        _action = (f, p) => Task.FromResult<IActionResult?>(func(f, p));
    }

    internal InProcessHttpFunctionAppBuilder(Func<TFunction, FunctionAppHttpInProcessCallProxy, Task<IActionResult>> func)
    {
        _action = async (f, p) => await func(f, p);
    }

    public Step<HttpResponseMessage> Call() =>
        new InProcessHttpFunctionAppTrigger<TFunction>(_action, _request.BuildVariable());

    public IFunctionAppHttpPayloadStage WithBody(VariableReference<string> text) { _request.SetBody(text); return this; }

    public IFunctionAppHttpPayloadStage WithBody(VariableReference<byte[]> data) { _request.SetBody(data); return this; }

    public IFunctionAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string> value)
    { _request.AddHeader(key, value); return this; }

    public IFunctionAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string[]> values)
    { _request.AddHeader(key, values); return this; }

    public IFunctionAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string>> headers)
    { _request.AddHeaders(headers); return this; }

    public IFunctionAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string[]>> headers)
    { _request.AddHeaders(headers); return this; }
}
