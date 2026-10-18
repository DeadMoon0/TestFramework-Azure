using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Extensions;
using TestFramework.Azure.FunctionApp;
using TestFramework.Azure.LogicApp;
using TestFramework.Azure.Runtime;
using TestFramework.Azure.ServiceBus;
using TestFramework.Config;
using TestFramework.Core.Environment;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Tests;

// README sync note: these tests mirror the public README samples for TestFramework.Azure.
// If you update a test here, update the corresponding README sample as well.
public class ReadmeSamplesTests
{
    [Fact]
    public async Task MinimalSetup_LoadAzureConfig_LetsARunAnswerForEachSection()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"azure-readme-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(tempFile, """
                {
                  "FunctionApp": {
                    "Default": {
                      "BaseUrl": "https://functions.test/",
                      "Code": "function-key"
                    }
                  },
                  "CosmosDb": {
                    "MainDb": {
                      "ConnectionString": "AccountEndpoint=https://cosmos.test/;",
                      "DatabaseName": "AppDb",
                      "ContainerName": "Orders"
                    }
                  },
                  "ServiceBus": {
                    "MainSBQueue": {
                      "ConnectionString": "Endpoint=sb://orders/",
                      "QueueName": "orders"
                    }
                  }
                }
                """);

            ConfigInstance config = ConfigInstance.FromJsonFile(tempFile)
                .LoadAzureConfig()
                .Build();

            await using ServiceProvider provider = config.BuildServiceProvider();
            RunContext run = RunContext.Detached(provider);

            Assert.Equal("https://functions.test/", run.Configured<FunctionAppConfig>("Default").BaseUrl);
            Assert.Equal("Orders", run.Configured<CosmosDbConfig>("MainDb").ContainerName);
            Assert.Equal("orders", run.Configured<ServiceBusConfig>("MainSBQueue").EntityName);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task FunctionAppHttpCall_RunsThroughSelectFunctionSample()
    {
        FakeHttpRequestSender sender = new(new HttpResponseMessage(HttpStatusCode.Accepted));
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.Declare("Default", new FunctionAppConfig
            {
                BaseUrl = "https://functions.test/",
                Code = "function-key",
            });
            services.AddSingleton(new FunctionAppTriggerConfig { DoPing = false });
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        Timeline timeline = Timeline.Create()
            .Trigger(AzureExt.Trigger.FunctionApp.Http("Default").SelectFunction("Run", HttpMethod.Post).Call())
            .Build();

        TimelineRun run = await timeline.SetupRun(runtime.ServiceProvider).RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal(new Uri("https://functions.test/api/Run"), sender.Request!.RequestUri);
        Assert.Equal(HttpMethod.Post, sender.Request!.Method);
    }

    [Fact]
    public async Task ManagedFunctionCall_RunsTheNamedFunctionThroughTheAdminEndpoint()
    {
        FakeHttpRequestSender sender = new(new HttpResponseMessage(HttpStatusCode.Accepted));
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.Declare("Default", new FunctionAppConfig
            {
                BaseUrl = "https://functions.test/",
                Code = "function-key",
                AdminCode = "master-key",
            });
            services.AddSingleton(new FunctionAppTriggerConfig { DoPing = false });
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        TimelineRun run = await Timeline.Create()
            .Trigger(AzureExt.Trigger.FunctionApp.ManagedFunction("Default", "NightlyCleanup"))
            .Build()
            .SetupRun(runtime.ServiceProvider)
            .RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal(new Uri("https://functions.test/admin/functions/NightlyCleanup"), sender.Request!.RequestUri);
        Assert.Equal(HttpMethod.Post, sender.Request!.Method);
        Assert.Equal("master-key", Assert.Single(sender.Request.Headers.GetValues("x-functions-key")));
    }

    [Fact]
    public async Task FunctionAppHttpCall_ExplicitEndpointBodyAndHeaders_ComposesExpectedRequest()
    {
        FakeHttpRequestSender sender = new(new HttpResponseMessage(HttpStatusCode.OK));
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.Declare("Default", new FunctionAppConfig
            {
                BaseUrl = "https://functions.test/",
                Code = "function-key",
            });
            services.AddSingleton(new FunctionAppTriggerConfig { DoPing = false });
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        Timeline timeline = Timeline.Create()
            .Trigger(
                AzureExt.Trigger.FunctionApp.Http("Default")
                    .SelectEndpoint(Var.Const("orders/42"), Var.Const(HttpMethod.Post))
                    .WithHeader(Var.Const("x-correlation-id"), Var.Const("order-42"))
                    .WithHeaders(Var.Const(new Dictionary<string, string> { ["x-tenant"] = "lab" }))
                    .WithBody(Var.Const("{\"id\":42}"))
                    .Call())
            .Build();

        TimelineRun run = await timeline.SetupRun(runtime.ServiceProvider).RunAsync();

        run.EnsureRanToCompletion();
        Assert.NotNull(sender.Request);
        Assert.Equal(HttpMethod.Post, sender.Request!.Method);
        Assert.Equal(new Uri("https://functions.test/api/orders/42"), sender.Request.RequestUri);
        Assert.Equal("function-key", Assert.Single(sender.Request.Headers.GetValues("x-functions-key")));
        Assert.Equal("order-42", Assert.Single(sender.Request.Headers.GetValues("x-correlation-id")));
        Assert.Equal("lab", Assert.Single(sender.Request.Headers.GetValues("x-tenant")));
        Assert.Equal("{\"id\":42}", Assert.Single(sender.RequestBodies));
    }

    [Fact]
    public void ServiceBusQueueSendWait_BuildsQueueSendAndReceiveSteps()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(AzureExt.Trigger.ServiceBus.Send("MainSBQueue", new ServiceBusMessage("Test message") { CorrelationId = "order-42" }))
            .WaitForEvent(AzureExt.Event.ServiceBus.MessageReceived("MainSBQueue").WithCorrelationId("order-42").CompleteMessage(true))
                .WithTimeout(TimeSpan.FromSeconds(10))
            .Build();

        Assert.NotNull(timeline);
    }

    [Fact]
    public void ServiceBusTopicTempSubscriptionWait_CreatesPreAndCleanupSteps()
    {
        ServiceBusProcessEvent receiveEvent = AzureExt.Event.ServiceBus.MessageReceived("MainSBTopic").WithCorrelationId("topic-1234").CompleteMessage(true).WithTempSubscription();
        Timeline timeline = Timeline.Create()
            .WaitForEvent(receiveEvent)
                .WithTimeout(TimeSpan.FromSeconds(10))
            .Trigger(AzureExt.Trigger.ServiceBus.Send("MainSBTopic", new ServiceBusMessage("Test message") { CorrelationId = "topic-1234" }))
            .Build();

        // A real store, not null!: the pre-step resolves the step timeout through it, so passing null
        // would hide a variable-backed timeout regression.
        VariableStore variableStore = TestVariableStore.Create();
        variableStore.SetVariable("sampleTimeout", TimeSpan.FromSeconds(10));
        receiveEvent.TimeoutOptions.Timeout = Var.Ref<TimeSpan>("sampleTimeout");

        Assert.NotNull(timeline);
        Assert.NotNull(((IHasPreStep)receiveEvent).CreatePreStep(variableStore));
        Assert.NotNull(((IHasCleanupStep)receiveEvent).CreateCleanupStep(variableStore));
    }

    [Fact]
    public async Task LogicAppStatelessCallAndCapture_ComposesExpectedRequestAndResult()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":\"https://logic.test/invoke/manual?sig=abc\"}")
            },
            new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("{\"message\":\"captured\"}")
            });

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.Declare("logic", new LogicAppConfig
            {
                WorkflowName = "StatelessOrders",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "https://logic.test/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            });
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        Timeline timeline = Timeline.Create()
            .Trigger(
                AzureExt.Trigger.LogicApp.Http("logic")
                    .Workflow("StatelessOrders")
                    .Manual()
                    .WithBody(Var.Const("{\"id\":42}"))
                    .CallAndCapture())
            .Label("logic-call")
            .Build();

        TimelineRun run = await timeline.SetupRun(runtime.ServiceProvider).RunAsync();

        run.EnsureRanToCompletion();
        LogicAppExecutionResult result = Assert.IsType<LogicAppExecutionResult>(run.Step("logic-call").LastResult.Result);
        Assert.Equal(LogicAppRunStatus.Succeeded, result.RunStatus);
        Assert.Equal("{\"message\":\"captured\"}", result.ResponseBody);
        // The real invoke must use the unredacted callback URL...
        Assert.Equal(new Uri("https://logic.test/invoke/manual?sig=abc"), sender.Requests[1].RequestUri);
        Assert.Equal("{\"id\":42}", sender.RequestBodies[1]);

        // ...while the result, which is rendered into logs, reports and the DebugUI, must not.
        Assert.Equal("https://logic.test/invoke/manual?sig=***redacted***", result.InvokeUri);
        Assert.DoesNotContain("sig=abc", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void LogicAppUriRedaction_RedactsSignatureParameters_AndLeavesTheRestIntact()
    {
        string? redacted = LogicAppUriRedaction.Redact(
            "https://logic.test/invoke/manual?api-version=2016-10-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=SECRET_TOKEN");

        Assert.Equal(
            "https://logic.test/invoke/manual?api-version=2016-10-01&sp=***redacted***&sv=***redacted***&sig=***redacted***",
            redacted);

        // No query string, nothing to redact.
        Assert.Equal("https://logic.test/invoke/manual", LogicAppUriRedaction.Redact("https://logic.test/invoke/manual"));

        // Idempotent, so results built from an already-redacted URL are unaffected.
        Assert.Equal(redacted, LogicAppUriRedaction.Redact(redacted));
    }

    [Fact]
    public async Task ConsumptionLogicAppRunTracking_RunsThroughTheDocumentedSnippet()
    {
        // Route-aware, because the invoke path may race a run-observation request against the invoke.
        ConsumptionRunTrackingSender sender = new("run-consumption-123");

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.Declare("ConsumptionOrders", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                HostingMode = LogicAppHostingMode.Consumption,
                Consumption = new LogicAppConsumptionConfig
                {
                    InvokeUrl = "https://logic-consumption.test/workflows/OrderProcessor/triggers/manual/paths/invoke?sig=abc",
                    WorkflowResourceId = "/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor",
                },
            });
            services.AddSingleton<ILogicAppConsumptionManagementRequestAuthorizer>(new StubConsumptionAuthorizer("test-token"));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new RouteAwareHttpComponentFactory(sender) });
        });

        Timeline timeline = Timeline.Create()
            .Trigger(
                AzureExt.Trigger.LogicApp.Http("ConsumptionOrders")
                    .Workflow("OrderProcessor")
                    .Manual()
                    .WithBody(Var.Const("{\"id\":42}"))
                    .Call())
            .Label("logic-call")
            .GetRunContext("logicRun")
            .WaitForEvent(
                AzureExt.Event.LogicApp.RunCompleted(
                    "ConsumptionOrders",
                    Var.Ref<LogicAppRunContext>("logicRun")))
            .Build();

        TimelineRun run = await timeline.SetupRun(runtime.ServiceProvider).RunAsync();

        run.EnsureRanToCompletion();
        Assert.Contains(sender.RequestUris, uri => uri == "https://logic-consumption.test/workflows/OrderProcessor/triggers/manual/paths/invoke?sig=abc");
        Assert.Contains(sender.RequestUris, uri => uri!.Contains("/runs/run-consumption-123", StringComparison.Ordinal));
    }

    [Fact]
    public void DocumentedLogicAppVerbs_Compile()
    {
        // The docs advertise Call() / CallAndCapture(). This test exists so the documented names
        // cannot silently drift from the code again.
        Step<LogicAppExecutionResult> call = AzureExt.Trigger.LogicApp
            .Http("logic")
            .Workflow("OrderProcessor")
            .Manual()
            .Call();

        Step<LogicAppExecutionResult> callAndCapture = AzureExt.Trigger.LogicApp
            .Http("logic")
            .Workflow("OrderProcessor")
            .Manual()
            .CallAndCapture();

        Assert.NotNull(call);
        Assert.NotNull(callAndCapture);
    }

    [Theory]
    [InlineData("AzureExt.Trigger.LogicApp.CallAndCapture(")]
    [InlineData("CallForRunContext")]
    [InlineData("LogicAppCapturedResult")]
    [InlineData(".GetCallbackUrl(")]
    [InlineData("AddAzure(configuration")]
    [InlineData("createTempSubscription:")]
    [InlineData("completeMessage:")]
    [InlineData(".GetSuccess(")]
    [InlineData("RegisterDefault<")]
    [InlineData("Step~HttpResponseMessage~")]
    [InlineData("ConvertToHttpResponseMessage")]
    [InlineData("configuration stores")]
    public void PackedReadme_DoesNotAdvertiseApisThatDoNotExist(string inventedApi)
    {
        // The README ships in the package, the skill file is what an assistant writes code from, and Arc42 is what a
        // contributor reads first, so none of them may name an API a stranger's compiler then refuses.
        string readme = FindPackedReadme();
        string repository = Path.GetDirectoryName(Path.GetDirectoryName(readme)!)!;
        string skill = Path.Combine(repository, "AI", "TestFramework.Azure.SKILL.md");
        string arc42 = Path.Combine(repository, "Documentation", "Arc42.md");

        foreach (string document in new[] { readme, skill, arc42 })
            Assert.DoesNotContain(inventedApi, File.ReadAllText(document), StringComparison.Ordinal);
    }

    private static string FindPackedReadme()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        for (int depth = 0; directory is not null && depth < 8; depth++)
        {
            string candidate = Path.Combine(directory.FullName, "TestFramework.Azure", "README.md");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the packed README next to the TestFramework.Azure project.");
    }

    /// <summary>
    /// Answers by route so the invoke request and the run-status polls cannot race a fixed queue.
    /// </summary>
    private sealed class ConsumptionRunTrackingSender(string runId) : IHttpRequestSender
    {
        private readonly System.Collections.Concurrent.ConcurrentBag<string?> requestUris = [];

        public IReadOnlyCollection<string?> RequestUris => [.. requestUris];

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string uri = request.RequestUri!.ToString();
            requestUris.Add(uri);

            if (uri.Contains($"/runs/{runId}", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"properties\":{\"status\":\"Succeeded\",\"code\":\"OK\"}}"),
                });
            }

            if (uri.Contains("/runs?", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"value\":[{{\"name\":\"{runId}\",\"properties\":{{\"status\":\"Succeeded\",\"code\":\"OK\"}}}}]}}"),
                });
            }

            HttpResponseMessage invokeResponse = new(HttpStatusCode.Accepted);
            invokeResponse.Headers.Add("x-ms-workflow-run-id", runId);
            return Task.FromResult(invokeResponse);
        }
    }

    private sealed class RouteAwareHttpComponentFactory(IHttpRequestSender sender) : IHttpComponentFactory
    {
        public IHttpRequestSender CreateSender() => sender;
    }

    private sealed class StubConsumptionAuthorizer(string token) : ILogicAppConsumptionManagementRequestAuthorizer
    {
        public Task AuthorizeAsync(TestFramework.Azure.Identifier.LogicAppIdentifier identifier, LogicAppConfig config, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CosmosClientOptionsNote_RegistersConfiguredClientOptionsProvider()
    {
        // The entry goes on the config instance rather than into a second configuration handed to the loader.
        // The old shape of this test is why AddAzure takes none: it passed a configuration
        // the run never saw, and only worked because the loader eagerly filled a store from it.
        ConfigInstance config = ConfigInstance.Create()
            .OverrideConfig(new Dictionary<string, string?>
            {
                ["CosmosDb:MainDb:ConnectionString"] = "AccountEndpoint=https://cosmos.test/;",
                ["CosmosDb:MainDb:DatabaseName"] = "AppDb",
                ["CosmosDb:MainDb:ContainerName"] = "Orders",
            })
            .AddService(services =>
            {
                services.AddAzure()
                    .ConfigureCosmosClientOptions(_ => new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                    });
            })
            .Build();

        await using ServiceProvider provider = config.BuildServiceProvider();
        ICosmosClientOptionsProvider optionsProvider = provider.GetRequiredService<ICosmosClientOptionsProvider>();
        CosmosDbConfig cosmosConfig = RunContext.Detached(provider).Configured<CosmosDbConfig>("MainDb");

        Assert.Equal(ConnectionMode.Gateway, optionsProvider.CreateOptions(cosmosConfig).ConnectionMode);
    }

    [Fact]
    public void FindDataArtifact_CreatesCosmosQueryFinderFromReadmeSample()
    {
        object finder = AzureExt.ArtifactFinder.DB.CosmosDbQuery<ReadmeCosmosItem>(
            "MainDb",
            new QueryDefinition("SELECT * FROM c WHERE c.number = 1"));

        Assert.NotNull(finder);
    }

    private sealed class ReadmeCosmosItem;

    private sealed class RuntimeContext
    {
        public IServiceProvider ServiceProvider { get; }

        private RuntimeContext(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public static RuntimeContext Create(Action<IServiceCollection> configureServices)
        {
            ServiceCollection services = new();
            configureServices(services);
            return new RuntimeContext(services.BuildServiceProvider());
        }
    }

    private sealed class FakeAzureComponentFactory : IAzureComponentFactory
    {
        public ICosmosComponentFactory Cosmos { get; } = new NotSupportedCosmosFactory();
        public IBlobComponentFactory Blob { get; } = new NotSupportedBlobFactory();
        public ITableComponentFactory Table { get; } = new NotSupportedTableFactory();
        public IServiceBusComponentFactory ServiceBus { get; } = new NotSupportedServiceBusFactory();
        public IHttpComponentFactory HttpFactory { get; init; } = null!;
        public IHttpComponentFactory Http => HttpFactory;
    }

    private sealed class FakeHttpComponentFactory(FakeHttpRequestSender sender) : IHttpComponentFactory
    {
        public IHttpRequestSender CreateSender() => sender;
    }

    private sealed class FakeHttpRequestSender(params HttpResponseMessage[] responses) : IHttpRequestSender
    {
        public HttpRequestMessage? Request { get; private set; }
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> RequestBodies { get; } = [];

        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Requests.Add(request);
            RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            HttpResponseMessage response = _responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.OK);
            return response;
        }
    }

    private sealed class NotSupportedCosmosFactory : ICosmosComponentFactory
    {
        public ICosmosContainerAdapter CreateContainer(Resolved<CosmosDbConfig> cosmos) => throw new NotSupportedException();
    }

    private sealed class NotSupportedBlobFactory : IBlobComponentFactory
    {
        public IBlobContainerAdapter CreateContainer(Resolved<StorageAccountConfig> account) => throw new NotSupportedException();
    }

    private sealed class NotSupportedTableFactory : ITableComponentFactory
    {
        public ITableAdapter CreateTable(Resolved<StorageAccountConfig> account, string tableName) => throw new NotSupportedException();
    }

    private sealed class NotSupportedServiceBusFactory : IServiceBusComponentFactory
    {
        public IServiceBusSenderAdapter CreateSender(Resolved<ServiceBusConfig> serviceBus) => throw new NotSupportedException();
        public IServiceBusMessagePump CreateMessagePump(Resolved<ServiceBusConfig> serviceBus, string? subscriptionName) => throw new NotSupportedException();
        public IServiceBusAdministrationAdapter CreateAdministration(Resolved<ServiceBusConfig> serviceBus) => throw new NotSupportedException();
    }
}