using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Http;
using Xunit;

namespace TestFramework.Azure.Tests;

/// <summary>
/// How HTTP messages are read and passed on: header names are case-insensitive, a repeated header is one
/// comma-joined value (Set-Cookie excepted, Cookie joined with "; "), a body survives being read and sent
/// more than once, and a copied request stands on its own.
/// </summary>
public class HttpMessageRulesTests
{
    private static HttpResponseMessage ResponseWithRepeatedHeaders()
    {
        HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        response.Headers.TryAddWithoutValidation("x-trace", "first");
        response.Headers.TryAddWithoutValidation("X-Trace", "second");
        response.Headers.TryAddWithoutValidation("Set-Cookie", "session=1; Expires=Wed, 21 Oct 2026 07:28:00 GMT");
        response.Headers.TryAddWithoutValidation("Set-Cookie", "theme=dark");
        return response;
    }

    [Fact]
    public void ResponseHeaderNames_AreFoundWhateverTheirCase()
    {
        using HttpResponseMessage response = ResponseWithRepeatedHeaders();

        IReadOnlyDictionary<string, string[]> headers = HttpShaping.ReadHeaders(response);

        Assert.True(headers.ContainsKey("X-TRACE"));
        Assert.True(headers.ContainsKey("content-type"));
        Assert.True(headers.ContainsKey("set-cookie"));
    }

    [Fact]
    public void ARepeatedResponseHeader_IsOneCommaJoinedValue()
    {
        using HttpResponseMessage response = ResponseWithRepeatedHeaders();

        IReadOnlyDictionary<string, string[]> headers = HttpShaping.ReadHeaders(response);

        Assert.Equal(["first, second"], headers["x-trace"]);
    }

    [Fact]
    public void EachSetCookie_IsKeptAsItsOwnValue()
    {
        using HttpResponseMessage response = ResponseWithRepeatedHeaders();

        IReadOnlyDictionary<string, string[]> headers = HttpShaping.ReadHeaders(response);

        Assert.Equal(["session=1; Expires=Wed, 21 Oct 2026 07:28:00 GMT", "theme=dark"], headers["Set-Cookie"]);
    }

    [Fact]
    public void ARequestsCookiePairs_AreJoinedAsACookieHeaderIs()
    {
        CommonHttpRequest request = HttpShaping.ComposeRequest(null, [new("Cookie", ["a=1", "b=2"])]);

        Assert.Equal(["a=1; b=2"], request.ReadHeaders()["cookie"]);
    }

    [Fact]
    public void AContentTypeHeader_DescribesTheBody_WhateverItsCase()
    {
        CommonHttpRequest request = HttpShaping.ComposeRequest(new StringContent("<a/>"), [new("content-TYPE", ["application/xml"])]);

        Assert.Equal("application/xml", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal(["application/xml"], request.ReadHeaders()["Content-Type"]);
    }

    [Fact]
    public async Task ARequestsBody_CanBeSentTwice()
    {
        CommonHttpRequest request = HttpShaping.ComposeRequest(new StringContent("payload", Encoding.UTF8, "text/plain"), []);

        using (HttpRequestMessage first = new(HttpMethod.Post, "https://example.test/"))
        {
            await HttpShaping.ApplyAsync(request, first, CancellationToken.None);
            Assert.Equal("payload", await first.Content!.ReadAsStringAsync());
        }

        using HttpRequestMessage second = new(HttpMethod.Post, "https://example.test/");
        await HttpShaping.ApplyAsync(request, second, CancellationToken.None);

        Assert.Equal("payload", await second.Content!.ReadAsStringAsync());
        Assert.Equal("payload", await second.Content.ReadAsStringAsync());
        Assert.Equal("text/plain", second.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task ACopiedRequest_IsNotChangedByWhatHappensToTheOriginal()
    {
        HttpRequestMessage original = new(HttpMethod.Put, "https://example.test/orders/7")
        {
            Content = new StringContent("order", Encoding.UTF8, "text/plain"),
        };
        original.Headers.Add("x-attempt", "1");

        using HttpRequestMessage copy = await HttpShaping.CloneRequestAsync(original, CancellationToken.None);

        original.Headers.Remove("x-attempt");
        original.Headers.Add("x-attempt", "2");
        original.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        original.Dispose();

        Assert.Equal(HttpMethod.Put, copy.Method);
        Assert.Equal("https://example.test/orders/7", copy.RequestUri!.ToString());
        Assert.Equal(["1"], copy.Headers.GetValues("x-attempt"));
        Assert.Equal("text/plain", copy.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("order", await copy.Content.ReadAsStringAsync());
    }
}
