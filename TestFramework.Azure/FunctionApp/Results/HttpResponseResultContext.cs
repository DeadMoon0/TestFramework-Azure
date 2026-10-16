using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Http;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.FunctionApp.Results;

/// <summary>
/// Serializable HTTP response context returned by Function App HTTP triggers.
/// </summary>
public sealed record HttpResponseResultContext : StepResultContext
{
    internal HttpResponseResultContext(HttpStatusCode statusCode, string? body, IReadOnlyDictionary<string, string[]> headers)
    {
        this.StatusCode = statusCode;
        this.Body = body;
        this.Headers = headers;
    }

    /// <summary>
    /// StatusCode
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Body
    /// </summary>
    public string? Body { get; }

    /// <summary>
    /// The response's headers, content headers included: names compare case-insensitively, a repeated header
    /// carries one comma-joined value, and only Set-Cookie keeps one value per cookie.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Headers { get; }

    /// <summary>
    /// The result a step reports for <paramref name="response"/>: its status, its body read as text, and its headers
    /// read by the rules every Function App and Logic App result uses.
    /// </summary>
    /// <param name="response">The response to read. It is not disposed.</param>
    /// <param name="cancellationToken">Cancels reading the body.</param>
    /// <returns>The result context for the response.</returns>
    public static async Task<HttpResponseResultContext> FromHttpResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? body = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return new HttpResponseResultContext(response.StatusCode, body, HttpShaping.ReadHeaders(response));
    }
}