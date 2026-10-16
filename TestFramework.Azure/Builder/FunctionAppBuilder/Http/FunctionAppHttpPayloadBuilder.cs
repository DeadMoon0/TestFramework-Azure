using System;
using System.Collections.Generic;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http;

/// <summary>
/// The payload stage of a Function App HTTP call: collects the body and headers, and hands the composed request to the
/// step that sends it.
/// </summary>
/// <remarks>
/// Every Function App HTTP call composes its request here - the remote one in this package, the in-process one in
/// <c>TestFramework.Azure.Functions</c> - so a body or a header means the same whichever way the call is made. Only
/// the step differs, and that is what the caller supplies.
/// </remarks>
public sealed class FunctionAppHttpPayloadBuilder : IFunctionAppHttpPayloadStage
{
    private readonly Func<VariableReference<CommonHttpRequest>, Step<HttpResponseResultContext>> _call;
    private readonly FunctionAppHttpRequestBuilderState _request = new();

    /// <summary>
    /// Starts a payload stage whose <see cref="Call"/> builds its step with <paramref name="call"/>.
    /// </summary>
    /// <param name="call">Builds the step that sends the request. It is given the request as a variable, resolved when the step runs.</param>
    public FunctionAppHttpPayloadBuilder(Func<VariableReference<CommonHttpRequest>, Step<HttpResponseResultContext>> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        _call = call;
    }

    /// <inheritdoc />
    public Step<HttpResponseResultContext> Call() => _call(_request.BuildVariable());

    /// <inheritdoc />
    public IFunctionAppHttpPayloadStage WithBody(VariableReference<string> text)
    {
        _request.SetBody(text);
        return this;
    }

    /// <inheritdoc />
    public IFunctionAppHttpPayloadStage WithBody(VariableReference<byte[]> data)
    {
        _request.SetBody(data);
        return this;
    }

    /// <inheritdoc />
    public IFunctionAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string> value)
    {
        _request.AddHeader(key, value);
        return this;
    }

    /// <inheritdoc />
    public IFunctionAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string[]> values)
    {
        _request.AddHeader(key, values);
        return this;
    }

    /// <inheritdoc />
    public IFunctionAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string>> headers)
    {
        _request.AddHeaders(headers);
        return this;
    }

    /// <inheritdoc />
    public IFunctionAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string[]>> headers)
    {
        _request.AddHeaders(headers);
        return this;
    }
}
