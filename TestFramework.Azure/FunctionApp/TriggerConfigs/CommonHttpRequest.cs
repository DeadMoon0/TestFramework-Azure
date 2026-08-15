using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
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
    /// Copies the headers and a fresh copy of the body onto <paramref name="message"/>.
    /// </summary>
    /// <remarks>
    /// The body is deliberately copied rather than shared. A <see cref="HttpRequestMessage"/> disposes
    /// its <see cref="HttpRequestMessage.Content"/>, so handing the same <see cref="HttpContent"/> to a
    /// second message - a step retry, or simply running a built timeline twice - would send a disposed
    /// stream. This requires re-readable content: everything the builders produce (StringContent,
    /// ByteArrayContent) is, while a user-supplied one-shot StreamContent is not and will now fail with
    /// a clear stream error instead of an ObjectDisposedException on the second send.
    /// </remarks>
    /// <param name="message">The message to configure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    internal async Task ApplyToHttpRequestMessageAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        foreach (var header in Headers)
        {
            message.Headers.Add(header.Key, header.Value);
        }

        if (Content is null)
        {
            return;
        }

        byte[] body = await Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        ByteArrayContent copy = new(body);

        foreach (KeyValuePair<string, IEnumerable<string>> header in Content.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        message.Content = copy;
    }
}