using System;
using System.Collections.Generic;
using System.Net.Http;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Http;
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
        HttpContent? body = bodyText is not null ? new StringContent(bodyText.GetRequiredValue(store))
            : bodyBytes is not null ? new ByteArrayContent(bodyBytes.GetRequiredValue(store))
            : null;

        return HttpShaping.ComposeRequest(body, ResolveHeaders(store));
    }

    private IEnumerable<KeyValuePair<string, IEnumerable<string>>> ResolveHeaders(VariableStore store)
    {
        foreach (var (k, v) in singleHeaders)
            yield return new(k.GetRequiredValue(store), [v.GetRequiredValue(store)]);

        foreach (var (k, v) in multiHeaders)
            yield return new(k.GetRequiredValue(store), v.GetRequiredValue(store));

        foreach (var dict in headersDicts)
            foreach (var (k, v) in dict.GetRequiredValue(store))
                yield return new(k, [v]);

        foreach (var dict in headersDictMultis)
            foreach (var (k, v) in dict.GetRequiredValue(store))
                yield return new(k, v);
    }

    public override VariableReference<TNew> Transform<TNew>(Func<CommonHttpRequest?, TNew?> transform) where TNew : default =>
        throw new NotSupportedException();

    public override VariableReference<TNew> Transform<TNew>(Func<CommonHttpRequest?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables) where TNew : default =>
        throw new NotSupportedException();
}
