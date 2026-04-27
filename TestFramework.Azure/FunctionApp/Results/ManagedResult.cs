using System.Net;

namespace TestFramework.Azure.FunctionApp.Results;

/// <summary>
/// Captures the HTTP-style result produced by a managed Function App invocation.
/// </summary>
public class ManagedResult
{
    /// <summary>
    /// The status code returned by the invoked function.
    /// </summary>
    public HttpStatusCode StatusCode { get; init; }

    /// <summary>
    /// Optional response body returned by the invoked function.
    /// </summary>
    public string? Body { get; init; }
}