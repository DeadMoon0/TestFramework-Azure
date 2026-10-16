using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;

namespace TestFramework.Azure.Functions.Tests;

// README sync note: these tests mirror the samples in the TestFramework.Azure.Functions README.
// If you update a test here, update the corresponding README sample as well.
public class ReadmeSamplesTests
{
    private static async Task<TimelineRun> RunAsync<TResult>(RecordingFunctionHost host, Step<TResult> step, string routePrefix = "api")
        where TResult : StepResultContext
    {
        await using ServiceProvider services = host.Services("Default", routePrefix);
        return await Timeline.Create()
            .Trigger(step)
            .Build()
            .SetupRun(services)
            .RunAsync();
    }

    [Fact]
    public async Task SelectEndpointWithMethod_CallsTheRouteAndVerbTheAttributesDeclare()
    {
        await using RecordingFunctionHost host = await RecordingFunctionHost.StartAsync();

        TimelineRun run = await RunAsync(host,
            AzureExt.Trigger.FunctionApp.Http("Default").SelectEndpointWithMethod<OrdersFunction>(nameof(OrdersFunction.Run)).Call());

        run.EnsureRanToCompletion();
        RecordingFunctionHost.Request request = Assert.Single(host.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/orders", request.Path);
        Assert.Equal("function-key", request.FunctionKey);
    }

    [Fact]
    public void SelectEndpointWithMethod_OnAMultiVerbTrigger_RefusesToGuessTheVerb()
    {
        FrameworkConfigurationException error = Assert.Throws<FrameworkConfigurationException>(() =>
            AzureExt.Trigger.FunctionApp.Http("Default")
                .SelectEndpointWithMethod<MultiVerbFunction>(nameof(MultiVerbFunction.Run)));

        Assert.Contains("get, post", error.Message, StringComparison.Ordinal);
        Assert.Contains("ambiguous", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectEndpointWithMethod_WithAVerbTheTriggerDoesNotDeclare_IsRefused()
    {
        FrameworkConfigurationException error = Assert.Throws<FrameworkConfigurationException>(() =>
            AzureExt.Trigger.FunctionApp.Http("Default")
                .SelectEndpointWithMethod<MultiVerbFunction>(nameof(MultiVerbFunction.Run), HttpMethod.Delete));

        Assert.Contains("not DELETE", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectEndpointWithMethod_OnAMultiVerbTrigger_TakesTheRouteFromTheAttributesAndTheVerbFromTheCaller()
    {
        await using RecordingFunctionHost host = await RecordingFunctionHost.StartAsync();

        TimelineRun run = await RunAsync(host,
            AzureExt.Trigger.FunctionApp.Http("Default")
                .SelectEndpointWithMethod<MultiVerbFunction>(nameof(MultiVerbFunction.Run), HttpMethod.Post)
                .Call());

        run.EnsureRanToCompletion();
        RecordingFunctionHost.Request request = Assert.Single(host.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/orders", request.Path);
    }

    [Fact]
    public async Task SelectEndpointWithMethod_WithoutARoute_CallsTheFunctionNameUnderTheHostPrefixOnce()
    {
        await using RecordingFunctionHost host = await RecordingFunctionHost.StartAsync();

        TimelineRun run = await RunAsync(host,
            AzureExt.Trigger.FunctionApp.Http("Default").SelectEndpointWithMethod<RoutelessFunction>(nameof(RoutelessFunction.Run)).Call());

        run.EnsureRanToCompletion();
        Assert.Equal("/api/Run", Assert.Single(host.Requests).Path);
    }

    [Fact]
    public async Task SelectEndpointWithMethod_WithAnEmptyRoutePrefix_CallsTheSiteRootDirectly()
    {
        await using RecordingFunctionHost host = await RecordingFunctionHost.StartAsync();

        TimelineRun run = await RunAsync(host,
            AzureExt.Trigger.FunctionApp.Http("Default").SelectEndpointWithMethod<OrdersFunction>(nameof(OrdersFunction.Run)).Call(),
            routePrefix: string.Empty);

        run.EnsureRanToCompletion();
        Assert.Equal("/orders", Assert.Single(host.Requests).Path);
    }

    [Fact]
    public void SelectEndpointWithMethod_OnAMethodWithoutTheFunctionAttribute_IsRefused()
    {
        FrameworkConfigurationException error = Assert.Throws<FrameworkConfigurationException>(() =>
            AzureExt.Trigger.FunctionApp.Http("Default")
                .SelectEndpointWithMethod<UnattributedFunction>(nameof(UnattributedFunction.Run)));

        Assert.Contains("[Function]", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Managed_RunsTheFunctionNamedInTheAttribute_NotTheMethod_ThroughTheAdminEndpoint()
    {
        await using RecordingFunctionHost host = await RecordingFunctionHost.StartAsync();

        TimelineRun run = await RunAsync(host,
            AzureExt.Trigger.FunctionApp.Managed<CleanupFunction>("Default", nameof(CleanupFunction.Run)));

        run.EnsureRanToCompletion();
        RecordingFunctionHost.Request request = Assert.Single(host.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/admin/functions/NightlyCleanup", request.Path);
        Assert.Equal("master-key", request.FunctionKey);
    }

    [Fact]
    public void Managed_OnAMethodWithoutTheFunctionAttribute_IsRefused()
    {
        Assert.Throws<FrameworkConfigurationException>(() =>
            AzureExt.Trigger.FunctionApp.Managed<UnattributedFunction>("Default", nameof(UnattributedFunction.Run)));
    }

    private sealed class OrdersFunction
    {
        [Function("Run")]
        public void Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] object request)
        {
        }
    }

    private sealed class MultiVerbFunction
    {
        [Function("Run")]
        public void Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "orders")] object request)
        {
        }
    }

    private sealed class RoutelessFunction
    {
        [Function("Run")]
        public void Run([HttpTrigger(AuthorizationLevel.Function, "post")] object request)
        {
        }
    }

    private sealed class CleanupFunction
    {
        [Function("NightlyCleanup")]
        public void Run()
        {
        }
    }

    private sealed class UnattributedFunction
    {
        public void Run([HttpTrigger(AuthorizationLevel.Function, "post")] object request)
        {
        }
    }
}
