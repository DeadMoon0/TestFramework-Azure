using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder;

internal sealed class ComposedHttpRequestVariable(
    VariableReference<string>? bodyText,
    VariableReference<byte[]>? bodyBytes,
    List<(VariableReference<string> key, VariableReference<string> value)> singleHeaders,
    List<(VariableReference<string> key, VariableReference<string[]> values)> multiHeaders,
    List<VariableReference<Dictionary<string, string>>> headersDicts,
    List<VariableReference<Dictionary<string, string[]>>> headersDictMultis)
    : VariableReference<CommonHttpRequest>
{
    public override bool RequireImmutability => false;
    public override bool HasIdentifier => false;
    public override VariableIdentifier? Identifier => null;

    public override CommonHttpRequest? GetValue(VariableStore store)
    {
        CommonHttpRequest request = new();

        if (bodyText is not null)
            request.Content = new StringContent(bodyText.GetRequiredValue(store));
        else if (bodyBytes is not null)
            request.Content = new ByteArrayContent(bodyBytes.GetRequiredValue(store));

        void ApplyHeader(string key, IEnumerable<string> values)
        {
            if (string.Equals(key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                request.Content ??= new ByteArrayContent([]);
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(values.First());
            }
            else
            {
                request.Headers.Add(key, values);
            }
        }

        foreach (var (k, v) in singleHeaders)
            ApplyHeader(k.GetRequiredValue(store), [v.GetRequiredValue(store)]);

        foreach (var (k, v) in multiHeaders)
            ApplyHeader(k.GetRequiredValue(store), v.GetRequiredValue(store));

        foreach (var dict in headersDicts)
            foreach (var (k, v) in dict.GetRequiredValue(store))
                ApplyHeader(k, [v]);

        foreach (var dict in headersDictMultis)
            foreach (var (k, v) in dict.GetRequiredValue(store))
                ApplyHeader(k, v);

        return request;
    }

    public override VariableReference<TNew> Transform<TNew>(Func<CommonHttpRequest?, TNew?> transform) where TNew : default =>
        throw new NotSupportedException();

    public override VariableReference<TNew> Transform<TNew>(Func<CommonHttpRequest?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables) where TNew : default =>
        throw new NotSupportedException();
}
