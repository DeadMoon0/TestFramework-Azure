using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.FunctionApp.TriggerConfigs;

namespace TestFramework.Azure.Http;

/// <summary>
/// The one place the pack shapes HTTP: composing a request, copying its body, cloning a message, and reading
/// headers back into a dictionary. Function App (remote and in-process) and Logic App calls all go through it,
/// so a request or a response looks the same whichever path produced it.
/// </summary>
internal static class HttpShaping
{
    /// <summary>
    /// Header names are case-insensitive in HTTP, so every header dictionary the pack hands out compares them so.
    /// </summary>
    internal static StringComparer HeaderNameComparer { get; } = StringComparer.OrdinalIgnoreCase;

    private const string SetCookie = "Set-Cookie";
    private const string Cookie = "Cookie";
    private const string ContentType = "Content-Type";

    /// <summary>
    /// Builds a request from a body and the headers given for it, in order.
    /// </summary>
    /// <remarks>
    /// A Content-Type header describes the body, so it lands on the content - an empty one when no body was
    /// given - instead of on the request, where HttpRequestHeaders would refuse it.
    /// </remarks>
    internal static CommonHttpRequest ComposeRequest(HttpContent? body, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        CommonHttpRequest request = new() { Content = body };

        foreach ((string name, IEnumerable<string> values) in headers)
        {
            if (HeaderNameComparer.Equals(name, ContentType))
            {
                request.Content ??= new ByteArrayContent([]);
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(values.First());
            }
            else
            {
                request.Headers.Add(name, values);
            }
        }

        return request;
    }

    /// <summary>
    /// Copies the request's headers and a fresh copy of its body onto <paramref name="message"/>.
    /// </summary>
    /// <remarks>
    /// The body is copied rather than shared. A <see cref="HttpRequestMessage"/> disposes its
    /// <see cref="HttpRequestMessage.Content"/>, so handing the same <see cref="HttpContent"/> to a second
    /// message - a step retry, or simply running a built timeline twice - would send a disposed stream. This
    /// requires re-readable content: everything the builders produce (StringContent, ByteArrayContent) is,
    /// while a user-supplied one-shot StreamContent is not and fails with a stream error on the second send.
    /// </remarks>
    internal static async Task ApplyAsync(CommonHttpRequest request, HttpRequestMessage message, CancellationToken cancellationToken)
    {
        foreach ((string name, IEnumerable<string> values) in request.Headers)
            message.Headers.Add(name, values);

        if (request.Content is not null)
            message.Content = await CopyContentAsync(request.Content, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A copy of <paramref name="source"/> that owns its own bytes and headers, so it can be read any number
    /// of times and outlives the original.
    /// </summary>
    internal static async Task<ByteArrayContent> CopyContentAsync(HttpContent source, CancellationToken cancellationToken)
    {
        byte[] body = await source.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        ByteArrayContent copy = new(body);

        foreach ((string name, IEnumerable<string> values) in source.Headers)
            copy.Headers.TryAddWithoutValidation(name, values);

        return copy;
    }

    /// <summary>
    /// A message that sends what <paramref name="source"/> would send and shares no mutable state with it.
    /// </summary>
    internal static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage source, CancellationToken cancellationToken)
    {
        HttpRequestMessage clone = new(source.Method, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy,
        };

        foreach ((string name, IEnumerable<string> values) in source.Headers)
            clone.Headers.TryAddWithoutValidation(name, values);

        if (source.Content is not null)
            clone.Content = await CopyContentAsync(source.Content, cancellationToken).ConfigureAwait(false);

        return clone;
    }

    /// <summary>
    /// The response's headers and its content's headers as one dictionary.
    /// </summary>
    internal static IReadOnlyDictionary<string, string[]> ReadHeaders(HttpResponseMessage response)
        => FlattenHeaders(response.Headers, response.Content?.Headers);

    /// <summary>
    /// The request's headers and its content's headers as one dictionary.
    /// </summary>
    internal static IReadOnlyDictionary<string, string[]> ReadHeaders(CommonHttpRequest request)
        => FlattenHeaders(request.Headers, request.Content?.Headers);

    /// <summary>
    /// One entry per header name, names compared case-insensitively, a repeated header combined as HTTP
    /// combines it.
    /// </summary>
    /// <remarks>
    /// RFC 9110 5.3: repeated field lines are equivalent to one line of their values joined with a comma, so a
    /// header read here carries a single value, the way a server sees it after the wire. Two exceptions, both
    /// from RFC 6265: Set-Cookie cannot be joined (a cookie's Expires holds a comma), so each cookie stays its
    /// own value; and a Cookie request header joins its pairs with "; ", as HttpClient sends it.
    /// </remarks>
    private static ReadOnlyDictionary<string, string[]> FlattenHeaders(HttpHeaders headers, HttpHeaders? contentHeaders)
    {
        Dictionary<string, List<string>> collected = new(HeaderNameComparer);

        foreach ((string name, IEnumerable<string> values) in contentHeaders is null ? headers : headers.Concat(contentHeaders))
        {
            if (!collected.TryGetValue(name, out List<string>? list))
                collected[name] = list = [];

            list.AddRange(values);
        }

        Dictionary<string, string[]> flattened = new(HeaderNameComparer);
        foreach ((string name, List<string> values) in collected)
            flattened[name] = Combine(name, values);

        return new ReadOnlyDictionary<string, string[]>(flattened);
    }

    private static string[] Combine(string name, List<string> values)
    {
        if (HeaderNameComparer.Equals(name, SetCookie))
            return [.. values];

        string separator = HeaderNameComparer.Equals(name, Cookie) ? "; " : ", ";
        return [string.Join(separator, values.Where(value => value.Length > 0))];
    }
}
