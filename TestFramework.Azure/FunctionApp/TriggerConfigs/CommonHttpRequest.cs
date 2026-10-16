using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using TestFramework.Azure.Http;
using TestFramework.Core;

namespace TestFramework.Azure.FunctionApp.TriggerConfigs;

/// <summary>
/// Mutable request model used while composing remote and in-process Function App HTTP calls.
/// </summary>
public class CommonHttpRequest : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the request has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Prevents further modifications to the request.
    /// </summary>
    public void Freeze() { IsFrozen = true; }

    /// <summary>
    /// The request headers that will be applied to the outgoing HTTP request.
    /// </summary>
    public HttpRequestHeaders Headers { get; } = new HttpRequestMessage().Headers; // Dispose not needed - no Content

    private HttpContent? _content = null;

    /// <summary>
    /// Optional body content that will be attached to the outgoing HTTP request.
    /// </summary>
    public HttpContent? Content { get => _content; set { ((IFreezable)this).EnsureNotFrozen(); _content = value; } }

    /// <summary>
    /// The request's headers, content headers included, as a server reads them after the wire.
    /// </summary>
    /// <remarks>
    /// Names compare case-insensitively and a repeated header carries one comma-joined value (a Cookie header's pairs
    /// joined with "; "). A request handed to code in-process is read through this, so that code sees the headers a
    /// deployed app would have received.
    /// </remarks>
    /// <returns>One entry per header name.</returns>
    public IReadOnlyDictionary<string, string[]> ReadHeaders() => HttpShaping.ReadHeaders(this);
}