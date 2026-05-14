using System.Net;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.FunctionApp.Results;

/// <summary>
/// Captures the HTTP-style result produced by a managed Function App invocation.
/// </summary>
public sealed record ManagedResult : StepResultContext
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