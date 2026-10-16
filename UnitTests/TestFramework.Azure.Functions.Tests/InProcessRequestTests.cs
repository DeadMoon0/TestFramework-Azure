using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Threading.Tasks;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Functions.Tests;

/// <summary>
/// What an in-process function is handed: the request a deployed app would have received for the same payload stage,
/// headers combined as the wire combines them, and the run's services.
/// </summary>
public class InProcessRequestTests
{
    public sealed class EchoFunction
    {
        public string Name => "echo";
    }

    private static async Task<TimelineRun> RunAsync(IFunctionAppHttpPayloadStage stage)
    {
        Timeline timeline = Timeline.Create().Trigger(stage.Call()).Label("call").Build();

        ServiceCollection services = new();
        services.AddSingleton<EchoFunction>();
        return await timeline.SetupRun(RunServices.Owned(services.BuildServiceProvider())).RunAsync();
    }

    [Fact]
    public async Task ARepeatedRequestHeader_IsReadAsOneCommaJoinedValue_WhateverTheNamesCase()
    {
        string? seen = null;

        TimelineRun run = await RunAsync(AzureExt.Trigger.FunctionApp
            .InProcessHttp<EchoFunction>((_, call) =>
            {
                seen = call.HttpRequest.Headers["X-TRACE"];
                return new OkResult();
            })
            .WithHeader(Var.Const("x-trace"), Var.Const<string[]>(["first", "second"])));

        run.EnsureRanToCompletion();
        Assert.Equal("first, second", seen);
    }

    [Fact]
    public async Task TheBody_ReachesTheFunctionAsAPost()
    {
        string? method = null;
        string? body = null;

        TimelineRun run = await RunAsync(AzureExt.Trigger.FunctionApp
            .InProcessHttp<EchoFunction>(async (_, call) =>
            {
                method = call.HttpRequest.Method;
                using StreamReader reader = new(call.HttpRequest.Body);
                body = await reader.ReadToEndAsync();
            })
            .WithBody(Var.Const("{\"id\":42}")));

        run.EnsureRanToCompletion();
        Assert.Equal("POST", method);
        Assert.Equal("{\"id\":42}", body);
    }

    [Fact]
    public async Task TheFunctionInstance_ComesFromTheRunsServices()
    {
        string? name = null;

        TimelineRun run = await RunAsync(AzureExt.Trigger.FunctionApp
            .InProcessHttp<EchoFunction>((function, call) =>
            {
                name = call.GetService<EchoFunction>() == function ? function.Name : null;
            }));

        run.EnsureRanToCompletion();
        Assert.Equal("echo", name);
    }
}
