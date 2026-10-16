using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TestFramework.Azure.Extensions;
using TestFramework.Azure.FunctionApp;
using TestFramework.Config;

namespace TestFramework.Azure.Functions.Tests;

/// <summary>
/// A local HTTP endpoint that stands in for a Function App host: it answers every request with 200 and records what
/// was asked of it.
/// </summary>
/// <remarks>
/// TestFramework.Azure keeps its HTTP sender to itself, so this suite cannot fake it; it sends to a real endpoint on the
/// loopback interface instead, which receives what a deployed host would.
/// </remarks>
internal sealed class RecordingFunctionHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<Request> _requests;

    private RecordingFunctionHost(WebApplication app, Uri baseUrl, ConcurrentQueue<Request> requests)
    {
        _app = app;
        BaseUrl = baseUrl;
        _requests = requests;
    }

    /// <summary>
    /// One request as the host received it.
    /// </summary>
    public sealed record Request(HttpMethod Method, string Path, string FunctionKey);

    public Uri BaseUrl { get; }

    public IReadOnlyList<Request> Requests => [.. _requests];

    public static async Task<RecordingFunctionHost> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();

        ConcurrentQueue<Request> requests = new();
        app.Run(context =>
        {
            requests.Enqueue(new Request(
                new HttpMethod(context.Request.Method),
                context.Request.Path.Value ?? string.Empty,
                context.Request.Headers["x-functions-key"].ToString()));
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        await app.StartAsync();
        return new RecordingFunctionHost(app, new Uri(app.Urls.Single().TrimEnd('/') + "/"), requests);
    }

    /// <summary>
    /// Services for a run whose Function App <paramref name="identifier"/> is this host.
    /// </summary>
    public ServiceProvider Services(string identifier, string routePrefix = "api")
    {
        // Declared the way a consumer declares it, as configuration keys, since that is the surface this package has.
        ConfigInstance config = ConfigInstance.Create()
            .OverrideConfig(new Dictionary<string, string?>
            {
                [$"FunctionApp:{identifier}:BaseUrl"] = BaseUrl.ToString(),
                [$"FunctionApp:{identifier}:RoutePrefix"] = routePrefix,
                [$"FunctionApp:{identifier}:Code"] = "function-key",
                [$"FunctionApp:{identifier}:AdminCode"] = "master-key",
            })
            .AddService(services =>
            {
                services.AddAzure();
                services.AddSingleton(new FunctionAppTriggerConfig { DoPing = false });
            })
            .Build();

        return (ServiceProvider)config.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
