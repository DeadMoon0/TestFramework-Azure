using System.Net.Http;
using System.Net.Http.Headers;
using TestFramework.Core;

namespace TestFramework.Azure.FunctionApp.TriggerConfigs;

public class CommonHttpRequest : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze() { IsFrozen = true; }

    public HttpRequestHeaders Headers { get; } = new HttpRequestMessage().Headers; // Dispose not needed - no Content

    private HttpContent? _content = null;
    public HttpContent? Content { get => _content; set { ((IFreezable)this).EnsureNotFrozen(); _content = value; } }

    internal void ApplyToHttpRequestMessage(HttpRequestMessage message)
    {
        foreach (var header in Headers)
        {
            message.Headers.Add(header.Key, header.Value);
        }

        message.Content = Content;
    }
}