using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;
using Xunit;

namespace TestFramework.Azure.Tests;

/// <summary>
/// What an in-process function call answers: the response the Functions host would have sent for the same result,
/// so a test can move from in-process to a deployed app without its assertions changing.
/// </summary>
public class InProcessHttpResultTests
{
    public sealed record Order(int Id, string Status);

    public sealed class OrderFunction
    {
    }

    private static async Task<TimelineRun> CallAsync(Func<OrderFunction, IActionResult> handler)
    {
        Timeline timeline = Timeline.Create()
            .Trigger(AzureExt.Trigger.FunctionApp.InProcessHttp<OrderFunction>((function, _) => handler(function)).Call()).Name("call")
            .Build();

        ServiceCollection services = new();
        services.AddSingleton<OrderFunction>();

        return await timeline.SetupRun(Core.Timelines.RunServices.Owned(services.BuildServiceProvider())).RunAsync();
    }

    private static HttpResponseResultContext Response(TimelineRun run)
    {
        run.EnsureRanToCompletion();
        return Assert.IsType<HttpResponseResultContext>(run.Step("call").LastResult.Result);
    }

    [Fact]
    public async Task AnObjectIsAnsweredAsJson_AsTheHostWould()
    {
        HttpResponseResultContext response = Response(await CallAsync(_ => new OkObjectResult(new Order(7, "open"))));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"id\":7,\"status\":\"open\"}", response.Body);
    }

    [Fact]
    public async Task AStringIsAnsweredAsText()
    {
        HttpResponseResultContext response = Response(await CallAsync(_ => new OkObjectResult("accepted")));

        Assert.Equal("accepted", response.Body);
    }

    [Fact]
    public async Task TheResultsStatusIsKept()
    {
        HttpResponseResultContext response = Response(await CallAsync(_ => new BadRequestObjectResult(new Order(7, "invalid"))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ContentKeepsItsContentType()
    {
        HttpResponseResultContext response = Response(await CallAsync(_ => new ContentResult { Content = "<p>hi</p>", ContentType = "text/html", StatusCode = 201 }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("<p>hi</p>", response.Body);
        Assert.Contains(response.Headers, header => header.Value is [var value] && value.StartsWith("text/html", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoResultIsAnEmptyOk()
    {
        HttpResponseResultContext response = Response(await CallAsync(_ => null!));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AResultTheHostCannotAnswer_FailsTheStepAsAConfigurationMistake()
    {
        TimelineRun run = await CallAsync(_ => new RedirectResult("https://example.invalid/"));

        Assert.IsType<FrameworkConfigurationException>(run.Step("call").LastResult.Exception);
    }
}
