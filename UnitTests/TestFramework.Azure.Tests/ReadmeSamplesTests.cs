using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Extensions;
using TestFramework.Azure.FunctionApp;
using TestFramework.Azure.LogicApp;
using TestFramework.Azure.Runtime;
using TestFramework.Azure.ServiceBus;
using TestFramework.Config;
using TestFramework.Core.Environment;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Tests;

public enum AuthorizationLevel
{
    Function,
}

public sealed class HttpTriggerAttribute(AuthorizationLevel authLevel, params string[] methods) : Attribute
{
    public AuthorizationLevel AuthorizationLevel { get; } = authLevel;

    public string[] Methods { get; } = methods;

    public string? Route { get; init; }
}

// README sync note: these tests mirror the public README samples for TestFramework.Azure.
// If you update a test here, update the corresponding README sample as well.
public class ReadmeSamplesTests
{
    [Fact]
    public void MinimalSetup_LoadAzureConfig_RegistersNamedAzureConfigStores()
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

            using ServiceProvider provider = (ServiceProvider)config.BuildServiceProvider();

            Assert.Equal("https://functions.test/", provider.GetRequiredService<ConfigStore<FunctionAppConfig>>().GetConfig("Default").BaseUrl);
            Assert.Equal("Orders", provider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig("MainDb").ContainerName);
            Assert.Equal("orders", provider.GetRequiredService<ConfigStore<ServiceBusConfig>>().GetConfig("MainSBQueue").EntityName);
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
    public async Task FunctionAppHttpCall_RunsThroughSelectEndpointWithMethodSample()
    {
        FakeHttpRequestSender sender = new(new HttpResponseMessage(HttpStatusCode.Accepted));
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<FunctionAppConfig>.Create("Default", new FunctionAppConfig
            {
                BaseUrl = "https://functions.test/api/",
                Code = "function-key",
            }));
            services.AddSingleton(new FunctionAppTriggerConfig { DoPing = false });
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        Timeline timeline = Timeline.Create()
            .Trigger(AzureExt.Trigger.FunctionApp.Http("Default").SelectEndpointWithMethod<ReadmeHttpFunction>(nameof(ReadmeHttpFunction.Run)).Call())
            .Build();

        TimelineRun run = await timeline.SetupRun(runtime.ServiceProvider).RunAsync();

        run.EnsureRanToCompletion();
        Assert.NotNull(sender.Request);
    }

    [Fact]
    public async Task FunctionAppHttpCall_ExplicitEndpointBodyAndHeaders_ComposesExpectedRequest()
    {
        FakeHttpRequestSender sender = new(new HttpResponseMessage(HttpStatusCode.OK));
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<FunctionAppConfig>.Create("Default", new FunctionAppConfig
            {
                BaseUrl = "https://functions.test/api/",
                Code = "function-key",
            }));
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
            .WaitForEvent(AzureExt.Event.ServiceBus.MessageReceived("MainSBQueue", correlationId: "order-42", completeMessage: true))
                .WithTimeOut(TimeSpan.FromSeconds(10))
            .Build();

        Assert.NotNull(timeline);
    }

    [Fact]
    public void ServiceBusTopicTempSubscriptionWait_CreatesPreAndCleanupSteps()
    {
        ServiceBusProcessEvent receiveEvent = AzureExt.Event.ServiceBus.MessageReceived("MainSBTopic", correlationId: "topic-1234", createTempSubscription: true, completeMessage: true);
        Timeline timeline = Timeline.Create()
            .WaitForEvent(receiveEvent)
                .WithTimeOut(TimeSpan.FromSeconds(10))
            .Trigger(AzureExt.Trigger.ServiceBus.Send("MainSBTopic", new ServiceBusMessage("Test message") { CorrelationId = "topic-1234" }))
            .Build();

        Assert.NotNull(timeline);
        Assert.NotNull(((IHasPreStep)receiveEvent).CreatePreStep(null!));
        Assert.NotNull(((IHasCleanupStep)receiveEvent).CreateCleanupStep(null!));
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
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "StatelessOrders",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "https://logic.test/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        Timeline timeline = Timeline.Create()
            .Trigger(
                AzureExt.Trigger.LogicApp.Http("logic")
                    .Workflow("StatelessOrders")
                    .Manual()
                    .WithBody(Var.Const("{\"id\":42}"))
                    .CallAndCapture())
            .Name("logic-call")
            .Build();

        TimelineRun run = await timeline.SetupRun(runtime.ServiceProvider).RunAsync();

        run.EnsureRanToCompletion();
        LogicAppCapturedResult result = Assert.IsType<LogicAppCapturedResult>(run.Step("logic-call").LastResult.Result);
        Assert.Equal(LogicAppRunStatus.Succeeded, result.Status);
        Assert.Equal("{\"message\":\"captured\"}", result.ResponseBody);
        Assert.Equal(new Uri("https://logic.test/invoke/manual?sig=abc"), sender.Requests[1].RequestUri);
        Assert.Equal("{\"id\":42}", sender.RequestBodies[1]);
    }

    [Fact]
    public void CosmosClientOptionsNote_RegistersConfiguredClientOptionsProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:MainDb:ConnectionString"] = "AccountEndpoint=https://cosmos.test/;",
                ["CosmosDb:MainDb:DatabaseName"] = "AppDb",
                ["CosmosDb:MainDb:ContainerName"] = "Orders",
            })
            .Build();

        ConfigInstance config = ConfigInstance.Create()
            .AddService((services, _) =>
            {
                services.LoadAzureConfigs(configuration)
                    .ConfigureCosmosClientOptions(_ => new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                    });
            })
            .Build();

        using ServiceProvider provider = (ServiceProvider)config.BuildServiceProvider();
        ICosmosClientOptionsProvider optionsProvider = provider.GetRequiredService<ICosmosClientOptionsProvider>();
        CosmosContainerDbConfig cosmosConfig = provider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig("MainDb");

        Assert.Equal(ConnectionMode.Gateway, optionsProvider.CreateOptions(cosmosConfig).ConnectionMode);
    }

    [Fact]
    public void FindDataArtifact_CreatesCosmosQueryFinderFromReadmeSample()
    {
        object finder = AzureExt.ArtifactFinder.DB.CosmosQuery<ReadmeCosmosItem>(
            "MainDb",
            new QueryDefinition("SELECT * FROM c WHERE c.number = 1"));

        Assert.NotNull(finder);
    }

    private sealed class ReadmeHttpFunction
    {
        [Function("Run")]
        public void Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] object request)
        {
        }
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
        public ICosmosContainerAdapter CreateContainer(CosmosContainerDbConfig config) => throw new NotSupportedException();
    }

    private sealed class NotSupportedBlobFactory : IBlobComponentFactory
    {
        public IBlobContainerAdapter CreateContainer(StorageAccountConfig config) => throw new NotSupportedException();
    }

    private sealed class NotSupportedTableFactory : ITableComponentFactory
    {
        public ITableAdapter CreateTable(StorageAccountConfig config, string tableName) => throw new NotSupportedException();
    }

    private sealed class NotSupportedServiceBusFactory : IServiceBusComponentFactory
    {
        public IServiceBusSenderAdapter CreateSender(ServiceBusConfig config) => throw new NotSupportedException();
        public IServiceBusMessagePump CreateMessagePump(ServiceBusConfig config, string? subscriptionName) => throw new NotSupportedException();
        public IServiceBusAdministrationAdapter CreateAdministration(ServiceBusConfig config) => throw new NotSupportedException();
    }
}