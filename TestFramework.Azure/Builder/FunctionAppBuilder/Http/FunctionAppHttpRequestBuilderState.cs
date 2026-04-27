using System.Collections.Generic;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http;

internal sealed class FunctionAppHttpRequestBuilderState
{
    private VariableReference<string>? _bodyText;
    private VariableReference<byte[]>? _bodyBytes;
    private readonly List<(VariableReference<string> key, VariableReference<string> value)> _singleHeaders = [];
    private readonly List<(VariableReference<string> key, VariableReference<string[]> values)> _multiHeaders = [];
    private readonly List<VariableReference<Dictionary<string, string>>> _headersDicts = [];
    private readonly List<VariableReference<Dictionary<string, string[]>>> _headersDictMultis = [];

    public void SetBody(VariableReference<string> text)
    {
        _bodyText = text;
    }

    public void SetBody(VariableReference<byte[]> data)
    {
        _bodyBytes = data;
    }

    public void AddHeader(VariableReference<string> key, VariableReference<string> value)
    {
        _singleHeaders.Add((key, value));
    }

    public void AddHeader(VariableReference<string> key, VariableReference<string[]> values)
    {
        _multiHeaders.Add((key, values));
    }

    public void AddHeaders(VariableReference<Dictionary<string, string>> headers)
    {
        _headersDicts.Add(headers);
    }

    public void AddHeaders(VariableReference<Dictionary<string, string[]>> headers)
    {
        _headersDictMultis.Add(headers);
    }

    public ComposedHttpRequestVariable BuildVariable()
    {
        return new ComposedHttpRequestVariable(_bodyText, _bodyBytes, _singleHeaders, _multiHeaders, _headersDicts, _headersDictMultis);
    }
}