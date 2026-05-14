using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.FunctionApp.Results;

/// <summary>
/// Serializable HTTP response context returned by Function App HTTP triggers.
/// </summary>
public sealed record HttpResponseResultContext(
    HttpStatusCode StatusCode,
    string? Body,
    IReadOnlyDictionary<string, string[]> Headers) : StepResultContext
{
    internal static async Task<HttpResponseResultContext> FromHttpResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? body = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<KeyValuePair<string, IEnumerable<string>>> contentHeaders = response.Content?.Headers
            ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>();

        Dictionary<string, string[]> headers = response.Headers
            .Concat(contentHeaders)
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.SelectMany(x => x.Value).ToArray(), StringComparer.OrdinalIgnoreCase);

        return new HttpResponseResultContext(response.StatusCode, body, new ReadOnlyDictionary<string, string[]>(headers));
    }
}