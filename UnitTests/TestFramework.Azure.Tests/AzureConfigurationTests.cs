using Azure.Data.Tables;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.DB.CosmosDB;
using TestFramework.Azure.DB.SqlServer;
using TestFramework.Azure.Exceptions;
using TestFramework.Azure.FunctionApp;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Azure.FunctionApp.Trigger;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.LogicApp;
using TestFramework.Azure.Runtime;
using TestFramework.Azure.ServiceBus;
using TestFramework.Azure.StorageAccount.Blob;
using TestFramework.Azure.StorageAccount.Table;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Tests;

public class AzureConfigurationTests
{
    [Fact]
    public void IdentifierRecords_RoundTripAsStrings()
    {
        AssertIdentifierRoundTrip("logic", value => (LogicAppIdentifier)value, id => (string)id);
        AssertIdentifierRoundTrip("func", value => (FunctionAppIdentifier)value, id => (string)id);
        AssertIdentifierRoundTrip("storage", value => (StorageAccountIdentifier)value, id => (string)id);
        AssertIdentifierRoundTrip("cosmos", value => (CosmosContainerIdentifier)value, id => (string)id);
        AssertIdentifierRoundTrip("bus", value => (ServiceBusIdentifier)value, id => (string)id);
        AssertIdentifierRoundTrip("sql", value => (SqlDatabaseIdentifier)value, id => (string)id);
    }

    [Fact]
    public void ConfigStore_AddConfig_LastWriteWins()
    {
        ConfigStore<string> store = new();

        store.AddConfig("primary", "first");
        store.AddConfig("primary", "second");

        Assert.Equal("second", store.GetConfig("primary"));
    }

    [Fact]
    public void DefaultConfigProvider_LoadsIdentifiersAndTypedValues()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["LogicApp:workflow:HostingMode"] = "Consumption",
            ["LogicApp:workflow:WorkflowName"] = "OrderProcessor",
            ["LogicApp:workflow:Standard:BaseUrl"] = "https://logic.test",
            ["LogicApp:workflow:Standard:Code"] = "logic-secret",
            ["LogicApp:workflow:Consumption:InvokeUrl"] = "https://logic-consumption.test/invoke?sig=abc",
            ["LogicApp:workflow:Consumption:WorkflowResourceId"] = "/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor",
            ["ServiceBus:orders:ConnectionString"] = "Endpoint=sb://orders/",
            ["ServiceBus:orders:QueueName"] = "orders",
            ["ServiceBus:orders:RequiredSession"] = "true",
            ["FunctionApp:notify:BaseUrl"] = "https://example.test",
            ["FunctionApp:notify:Code"] = "secret",
        });

        DefaultConfigProvider provider = new();
    LogicAppConfig logicAppConfig = provider.LoadLogicAppConfig(configuration, "workflow");
        ServiceBusConfig busConfig = provider.LoadServiceBusConfig(configuration, "orders");
        FunctionAppConfig functionAppConfig = provider.LoadFunctionAppConfig(configuration, "notify");
        CosmosContainerDbConfig cosmosConfig = provider.LoadCosmosDbConfig(BuildConfiguration(new Dictionary<string, string?>
        {
            ["CosmosDb:cosmos:ConnectionString"] = "AccountEndpoint=https://cosmos.test/",
            ["CosmosDb:cosmos:DatabaseName"] = "db",
            ["CosmosDb:cosmos:ContainerName"] = "items",
        }), "cosmos");

        Assert.Equal(new[] { "workflow" }, provider.LoadAllLogicAppIdentifier(configuration));
        Assert.Equal(new[] { "orders" }, provider.LoadAllServiceBusIdentifier(configuration));
        Assert.Equal(new[] { "notify" }, provider.LoadAllFunctionAppIdentifier(configuration));
        Assert.Equal("https://logic.test", logicAppConfig.Standard.BaseUrl);
        Assert.Equal(LogicAppHostingMode.Consumption, logicAppConfig.HostingMode);
        Assert.Equal("OrderProcessor", logicAppConfig.WorkflowName);
        Assert.Equal("logic-secret", logicAppConfig.Standard.Code);
        Assert.Equal("https://logic-consumption.test/invoke?sig=abc", logicAppConfig.Consumption.InvokeUrl);
        Assert.Equal("/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor", logicAppConfig.Consumption.WorkflowResourceId);
        Assert.Equal("Endpoint=sb://orders/", busConfig.ConnectionString);
        Assert.Equal("orders", busConfig.EntityName);
        Assert.True(busConfig.RequiredSession);
        Assert.Equal("https://example.test", functionAppConfig.BaseUrl);
        Assert.Equal("secret", functionAppConfig.Code);
        Assert.Equal("items", cosmosConfig.ContainerName);
    }

    [Fact]
    public void DefaultConfigProvider_ThrowsWhenRequiredFieldIsMissing()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["FunctionApp:notify:Code"] = "secret",
        });

        DefaultConfigProvider provider = new();

        ConfigurationValidationException exception = Assert.Throws<ConfigurationValidationException>(() => provider.LoadFunctionAppConfig(configuration, "notify"));

        Assert.Contains("BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigLoader_LoadAllConfigs_RegistersTypedStores()
    {
        ServiceCollection services = new();
        ConfigLoader loader = new(new StubConfigProvider());

        loader.LoadAllConfigs(BuildConfiguration(new Dictionary<string, string?>()), services);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Equal("https://logic.test", provider.GetRequiredService<ConfigStore<LogicAppConfig>>().GetConfig("logic-app").Standard.BaseUrl);
        Assert.Equal("OrderProcessor", provider.GetRequiredService<ConfigStore<LogicAppConfig>>().GetConfig("logic-app").WorkflowName);
        Assert.Equal("https://functions.test", provider.GetRequiredService<ConfigStore<FunctionAppConfig>>().GetConfig("func").BaseUrl);
        Assert.Equal("db", provider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig("cosmos").DatabaseName);
        Assert.Equal("queue", provider.GetRequiredService<ConfigStore<ServiceBusConfig>>().GetConfig("bus").EntityName);
        Assert.Equal("blob", provider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig("storage").BlobContainerNameRequired);
        Assert.Equal("main", provider.GetRequiredService<ConfigStore<SqlDatabaseConfig>>().GetConfig("sql").DatabaseName);
    }

    [Fact]
    public void DefaultConfigExporter_ExportsSectionShapedKeys()
    {
        DefaultConfigExporter exporter = new();

        IReadOnlyDictionary<string, string> logicApp = exporter.ExportLogicAppConfig("logic", new LogicAppConfig
        {
            HostingMode = LogicAppHostingMode.Consumption,
            WorkflowName = "OrderProcessor",
            Standard = new LogicAppStandardConfig
            {
                BaseUrl = "https://logic.test",
                Code = "runtime-code",
                AdminCode = "admin-code",
            },
            Consumption = new LogicAppConsumptionConfig
            {
                InvokeUrl = "https://logic-consumption.test/invoke?sig=abc",
                WorkflowResourceId = "/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor",
            },
        });

        IReadOnlyDictionary<string, string> cosmos = exporter.ExportCosmosDbConfig("cosmos", new CosmosContainerDbConfig
        {
            ConnectionString = "AccountEndpoint=https://cosmos.test/",
            DatabaseName = "db",
            ContainerName = "items",
        });
        IReadOnlyDictionary<string, string> serviceBus = exporter.ExportServiceBusConfig("bus", new ServiceBusConfig
        {
            ConnectionString = "Endpoint=sb://localhost/;UseDevelopmentEmulator=true;",
            QueueName = null,
            TopicName = "orders",
            SubscriptionName = "Default",
            RequiredSession = false,
        });

        Assert.Equal("https://logic.test", logicApp["LogicApp:logic:Standard:BaseUrl"]);
        Assert.Equal("Consumption", logicApp["LogicApp:logic:HostingMode"]);
        Assert.Equal("OrderProcessor", logicApp["LogicApp:logic:WorkflowName"]);
        Assert.Equal("runtime-code", logicApp["LogicApp:logic:Standard:Code"]);
        Assert.Equal("admin-code", logicApp["LogicApp:logic:Standard:AdminCode"]);
        Assert.Equal("https://logic-consumption.test/invoke?sig=abc", logicApp["LogicApp:logic:Consumption:InvokeUrl"]);
        Assert.Equal("/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor", logicApp["LogicApp:logic:Consumption:WorkflowResourceId"]);
        Assert.Equal("AccountEndpoint=https://cosmos.test/", cosmos["CosmosDb:cosmos:ConnectionString"]);
        Assert.Equal("db", cosmos["CosmosDb:cosmos:DatabaseName"]);
        Assert.Equal("items", cosmos["CosmosDb:cosmos:ContainerName"]);
        Assert.Equal("Endpoint=sb://localhost/;UseDevelopmentEmulator=true;", serviceBus["ServiceBus:bus:ConnectionString"]);
        Assert.Equal("orders", serviceBus["ServiceBus:bus:TopicName"]);
        Assert.Equal("Default", serviceBus["ServiceBus:bus:SubscriptionName"]);
        Assert.Equal(bool.FalseString, serviceBus["ServiceBus:bus:RequiredSession"]);
    }

    [Fact]
    public async Task ServiceBusSendTrigger_UsesUnifiedFactoryAndAssignsSessionId()
    {
        FakeServiceBusSender sender = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<ServiceBusConfig>.Create("bus", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = "orders",
                TopicName = null,
                SubscriptionName = null,
                RequiredSession = true,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { ServiceBusFactory = new FakeServiceBusComponentFactory(sender: sender) });
        });

        ServiceBusMessage message = new("payload");
        ServiceBusSendTrigger trigger = new("bus", Var.Const(message));

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Same(message, sender.MessageSent);
        Assert.False(string.IsNullOrWhiteSpace(message.SessionId));
    }

    [Fact]
    public async Task HttpRemoteFunctionAppTrigger_UsesUnifiedFactorySender()
    {
        HttpResponseMessage response = new(HttpStatusCode.Accepted);
        FakeHttpRequestSender sender = new(response);
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<FunctionAppConfig>.Create("func", new FunctionAppConfig
            {
                BaseUrl = "https://example.test/api/",
                Code = "secret",
                AdminCode = null,
            }));
            services.AddSingleton(new FunctionAppTriggerConfig { DoPing = false });
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        TriggerHttpRouting routing = new("orders", HttpMethod.Post, Var.Const(new Dictionary<string, string>()));
        CommonHttpRequest request = new();
        request.Headers.Add("x-test", "1");
        request.Content = new StringContent("body", Encoding.UTF8, "text/plain");

        HttpRemoteFunctionAppTrigger trigger = new("func", Var.Const(routing), Var.Const(request));

        var actual = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(HttpStatusCode.Accepted, actual!.StatusCode);
        Assert.Equal(string.Empty, actual.Body);
        Assert.Equal("https://example.test/api/orders", sender.Request!.RequestUri!.ToString());
        Assert.Equal("secret", sender.Request.Headers.GetValues("x-functions-key").Single());
        Assert.Equal("1", sender.Request.Headers.GetValues("x-test").Single());
    }

    [Fact]
    public async Task FunctionAppHttpBuilder_ComposesBodyAndHeadersThroughExecution()
    {
        FakeHttpRequestSender sender = new(new HttpResponseMessage(HttpStatusCode.OK));
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<FunctionAppConfig>.Create("func", new FunctionAppConfig
            {
                BaseUrl = "https://example.test/api/",
                Code = "secret",
                AdminCode = null,
            }));
            services.AddSingleton(new FunctionAppTriggerConfig { DoPing = false });
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        var trigger = AzureExt.Trigger.FunctionApp
            .Http("func")
            .SelectEndpoint(Var.Const("orders"), Var.Const(HttpMethod.Post))
            .WithBody(Var.Const("payload"))
            .WithHeader(Var.Const("Content-Type"), Var.Const("application/json"))
            .WithHeader(Var.Const("x-test"), Var.Const("1"))
            .Call();

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Equal("https://example.test/api/orders", sender.Requests.Single().RequestUri!.ToString());
        Assert.Equal("1", sender.Requests.Single().Headers.GetValues("x-test").Single());
        Assert.Equal("application/json", sender.Requests.Single().Content!.Headers.ContentType!.MediaType);
        Assert.Equal("payload", await sender.Requests.Single().Content!.ReadAsStringAsync());
    }

    [Fact]
    public async Task FunctionAppSelectFunction_PrefixesDefaultApiRoute()
    {
        FakeHttpRequestSender sender = new(new HttpResponseMessage(HttpStatusCode.OK));
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<FunctionAppConfig>.Create("func", new FunctionAppConfig
            {
                BaseUrl = "https://example.test/",
                Code = "secret",
                AdminCode = null,
            }));
            services.AddSingleton(new FunctionAppTriggerConfig { DoPing = false });
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        var trigger = AzureExt.Trigger.FunctionApp
            .Http("func")
            .SelectFunction("HttpEchoTest", HttpMethod.Post)
            .Call();

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Equal("https://example.test/api/HttpEchoTest", sender.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task HttpRemoteFunctionAppTrigger_RetriesLocalhostNotFoundDuringWarmup()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.NotFound),
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<FunctionAppConfig>.Create("func", new FunctionAppConfig
            {
                BaseUrl = "http://localhost:7071/",
                Code = "secret",
                AdminCode = null,
            }));
            services.AddSingleton(new FunctionAppTriggerConfig
            {
                DoPing = false,
                LocalNotFoundRetryDuration = TimeSpan.FromSeconds(1),
                LocalNotFoundRetryDelay = TimeSpan.Zero,
            });
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        TriggerHttpRouting routing = new("api/orders", HttpMethod.Post, Var.Const(new Dictionary<string, string>()));
        CommonHttpRequest request = new();

        HttpRemoteFunctionAppTrigger trigger = new("func", Var.Const(routing), Var.Const(request));

        var actual = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        Assert.Equal(2, sender.Requests.Count);
        Assert.All(sender.Requests, loggedRequest => Assert.Equal("http://localhost:7071/api/orders", loggedRequest.RequestUri!.ToString()));
    }

    [Fact]
    public async Task LogicAppHttpTrigger_ResolvesCallbackUrlAndInvokesWorkflow()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":\"https://logic.test/invoke/manual?sig=abc\"}")
            },
            new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("accepted")
            });
        sender.Responses[1].Headers.Add("x-ms-workflow-run-id", "run-123");

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "https://logic.test/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        Step<LogicAppTriggerResult> trigger = AzureExt.Trigger.LogicApp
            .Http("logic")
            .Workflow("OrderProcessor")
            .Manual()
            .WithBody(Var.Const("payload"))
            .WithHeader(Var.Const("x-test"), Var.Const("1"))
            .Call();

        LogicAppTriggerResult? result = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("OrderProcessor", result!.WorkflowName);
        Assert.Equal("manual", result.TriggerName);
        Assert.Equal("run-123", result.RunId);
        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        Assert.Equal("https://logic.test/runtime/webhooks/workflow/api/management/workflows/OrderProcessor/triggers/manual/listCallbackUrl?api-version=2022-03-01", sender.Requests[0].RequestUri!.ToString());
        Assert.Equal("logic-admin", sender.Requests[0].Headers.GetValues("x-functions-key").Single());
        Assert.Equal("https://logic.test/invoke/manual?sig=abc", sender.Requests[1].RequestUri!.ToString());
        Assert.Equal("1", sender.Requests[1].Headers.GetValues("x-test").Single());
        Assert.Equal("payload", await sender.Requests[1].Content!.ReadAsStringAsync());
    }

    [Fact]
    public async Task LogicAppHttpTrigger_RebasesLocalCallbackUrl_ToConfiguredBaseUrl()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":\"https://localhost:443/api/OrderProcessor/triggers/manual/invoke?api-version=2022-05-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=abc\"}")
            },
            new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("accepted")
            });
        sender.Responses[1].Headers.Add("x-ms-workflow-run-id", "run-local-123");

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "http://127.0.0.1:59911/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        Step<LogicAppTriggerResult> trigger = AzureExt.Trigger.LogicApp
            .Http("logic")
            .Workflow("OrderProcessor")
            .Manual()
            .WithBody(Var.Const("payload"))
            .Call();

        LogicAppTriggerResult? result = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("http://127.0.0.1:59911/api/OrderProcessor/triggers/manual/invoke?api-version=2022-05-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=abc", sender.Requests[1].RequestUri!.ToString());
        Assert.Equal("http://127.0.0.1:59911/api/OrderProcessor/triggers/manual/invoke?api-version=2022-05-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=abc", result!.CallbackUrl);
        Assert.Equal("run-local-123", result.RunId);
    }

    [Fact]
    public async Task LogicAppHttpCaptureTrigger_ReturnsCapturedCallbackResponse()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":\"https://localhost:443/api/OrderProcessor/triggers/manual/invoke?api-version=2022-05-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=abc\"}")
            },
            new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("{\"message\":\"captured\"}")
            });
        sender.Responses[1].Headers.Add("x-ms-workflow-run-id", "run-capture-123");
        sender.Responses[1].Content!.Headers.Add("x-capture", "true");

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "http://127.0.0.1:59911/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        Step<LogicAppCapturedResult> trigger = AzureExt.Trigger.LogicApp
            .Http("logic")
            .Workflow("OrderProcessor")
            .Manual()
            .WithBody(Var.Const("payload"))
            .CallAndCapture();

        LogicAppCapturedResult? result = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("OrderProcessor", result!.WorkflowName);
        Assert.Equal("manual", result.TriggerName);
        Assert.Equal("run-capture-123", result.RunId);
        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        Assert.Equal(LogicAppRunStatus.Succeeded, result.Status);
        Assert.Equal("{\"message\":\"captured\"}", result.ResponseBody);
        Assert.Equal("true", Assert.Single(result.ResponseHeaders["x-capture"]));
        Assert.Equal("http://127.0.0.1:59911/api/OrderProcessor/triggers/manual/invoke?api-version=2022-05-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=abc", result.CallbackUrl);
    }

    [Fact]
    public async Task LogicAppHttpCaptureTrigger_MapsFailedHttpResponse_ToFailedStatus()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":\"https://logic.test/invoke/manual?sig=abc\"}")
            },
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("bad request")
            });

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "https://logic.test/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        Step<LogicAppCapturedResult> trigger = AzureExt.Trigger.LogicApp
            .Http("logic")
            .Workflow("OrderProcessor")
            .Manual()
            .CallAndCapture();

        LogicAppCapturedResult? result = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.BadRequest, result!.StatusCode);
        Assert.Equal(LogicAppRunStatus.Failed, result.Status);
        Assert.Equal("bad request", result.ResponseBody);
    }

    [Fact]
    public async Task LogicAppHttpTrigger_UsesConsumptionCallbackUrl_WhenConfigured()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("accepted")
            });
        sender.Responses[0].Headers.Add("x-ms-workflow-run-id", "run-consumption-123");

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                HostingMode = LogicAppHostingMode.Consumption,
                Consumption = new LogicAppConsumptionConfig
                {
                    InvokeUrl = "https://logic-consumption.test/workflows/OrderProcessor/triggers/manual/paths/invoke?sig=abc",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        Step<LogicAppTriggerResult> trigger = AzureExt.Trigger.LogicApp
            .Http("logic")
            .Workflow("OrderProcessor")
            .Manual()
            .WithBody(Var.Const("payload"))
            .Call();

        LogicAppTriggerResult? result = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("https://logic-consumption.test/workflows/OrderProcessor/triggers/manual/paths/invoke?sig=abc", sender.Requests[0].RequestUri!.ToString());
        Assert.Equal("run-consumption-123", result!.RunId);
        Assert.Equal("https://logic-consumption.test/workflows/OrderProcessor/triggers/manual/paths/invoke?sig=abc", result.CallbackUrl);
    }

    [Fact]
    public async Task LogicAppTimerTrigger_UsesConsumptionWorkflowResourceId()
    {
        FakeHttpRequestSender sender = new(new HttpResponseMessage(HttpStatusCode.Accepted));
        sender.Responses[0].Headers.Add("x-ms-workflow-run-id", "run-consumption-timer-123");

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "NightlyJob",
                HostingMode = LogicAppHostingMode.Consumption,
                Consumption = new LogicAppConsumptionConfig
                {
                    WorkflowResourceId = "/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/NightlyJob",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
            services.AddSingleton<ILogicAppConsumptionManagementRequestAuthorizer>(new StubLogicAppConsumptionManagementRequestAuthorizer("test-token"));
        });

        Step<LogicAppTriggerResult> trigger = AzureExt.Trigger.LogicApp
            .Http("logic")
            .Workflow("NightlyJob")
            .Timer()
            .Call();

        LogicAppTriggerResult? result = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("https://management.azure.com/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/NightlyJob/triggers/Recurrence/run?api-version=2019-05-01", sender.Requests[0].RequestUri!.ToString());
        Assert.Equal("Bearer", sender.Requests[0].Headers.Authorization!.Scheme);
        Assert.Equal("test-token", sender.Requests[0].Headers.Authorization!.Parameter);
        Assert.Equal("run-consumption-timer-123", result!.RunId);
    }

    [Fact]
    public async Task LogicAppTimerTrigger_RunsManagementTriggerEndpoint()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.Accepted));
        sender.Responses[0].Headers.Add("x-ms-workflow-run-id", "run-timer-123");

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "NightlyJob",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "https://logic.test/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        Step<LogicAppTriggerResult> trigger = AzureExt.Trigger.LogicApp
            .Http("logic")
            .Workflow("NightlyJob")
            .Timer()
            .Call();

        LogicAppTriggerResult? result = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("NightlyJob", result!.WorkflowName);
        Assert.Equal("Recurrence", result.TriggerName);
        Assert.Equal("run-timer-123", result.RunId);
        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        Assert.Equal("https://logic.test/runtime/webhooks/workflow/api/management/workflows/NightlyJob/triggers/Recurrence/run?api-version=2022-03-01", sender.Requests[0].RequestUri!.ToString());
        Assert.Equal("logic-admin", sender.Requests[0].Headers.GetValues("x-functions-key").Single());
        Assert.Null(sender.Requests[0].Content);
    }

        [Fact]
        public async Task LogicAppTimerTrigger_FallsBackToObservedScheduledRun_WhenTriggerRunIsUnavailable()
        {
                FakeHttpRequestSender sender = new(
                        new HttpResponseMessage(HttpStatusCode.NotFound),
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                                Content = new StringContent("""
                                {
                                    "value": [
                                        {
                                            "name": "run-observed-123",
                                            "properties": {
                                                "startTime": "2099-01-01T00:00:01Z",
                                                "trigger": {
                                                    "name": "Recurrence"
                                                }
                                            }
                                        }
                                    ]
                                }
                                """)
                        });

                RuntimeContext runtime = RuntimeContext.Create(services =>
                {
                        services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
                        {
                                WorkflowName = "NightlyJob",
                        Standard = new LogicAppStandardConfig
                        {
                            BaseUrl = "https://logic.test/",
                            Code = "logic-code",
                            AdminCode = "logic-admin",
                        },
                        }));
                        services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
                });

                Step<LogicAppTriggerResult> trigger = AzureExt.Trigger.LogicApp
                        .Http("logic")
                        .Workflow("NightlyJob")
                        .Timer()
                        .Call();

                LogicAppTriggerResult? result = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

                Assert.NotNull(result);
                Assert.Equal("run-observed-123", result!.RunId);
                Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
                Assert.Equal("https://logic.test/runtime/webhooks/workflow/api/management/workflows/NightlyJob/triggers/Recurrence/run?api-version=2022-03-01", sender.Requests[0].RequestUri!.ToString());
                Assert.Equal("https://logic.test/runtime/webhooks/workflow/api/management/workflows/NightlyJob/runs?api-version=2022-03-01", sender.Requests[1].RequestUri!.ToString());
        }

    [Fact]
    public async Task LogicAppRunEvent_ReturnsWhenExpectedStatusIsReached()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"properties\":{\"status\":\"Running\"}}")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"properties\":{\"status\":\"Succeeded\",\"code\":\"OK\",\"outputs\":{\"result\":\"done\"}}}")
            });

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "https://logic.test/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        var evt = AzureExt.Event.LogicApp.RunReachedStatus("logic", Var.Const("run-123"), LogicAppRunStatus.Succeeded, Var.Const("OrderProcessor"));

        LogicAppRunDetails? result = await evt.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(LogicAppRunStatus.Succeeded, result!.Status);
        Assert.Equal("OK", result.Code);
        Assert.Equal("{\"result\":\"done\"}", result.OutputsJson);
        Assert.Equal(2, sender.Requests.Count);
        Assert.Equal("https://logic.test/runtime/webhooks/workflow/api/management/workflows/OrderProcessor/runs/run-123?api-version=2022-03-01", sender.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task LogicAppRunEvent_RetriesUntilRunBecomesAvailable()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.NotFound),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":[]}")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"properties\":{\"status\":\"Succeeded\",\"code\":\"OK\"}}")
            });

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "https://logic.test/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        var evt = AzureExt.Event.LogicApp.RunCompleted("logic", Var.Const("run-123"), Var.Const("OrderProcessor"));

        LogicAppRunDetails? result = await evt.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(LogicAppRunStatus.Succeeded, result!.Status);
        Assert.Equal(3, sender.Requests.Count);
        Assert.Equal("https://logic.test/runtime/webhooks/workflow/api/management/workflows/OrderProcessor/runs?api-version=2022-03-01", sender.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task LogicAppRunEvent_AllowsCapturedRunContextVariableInTimeline()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"properties\":{\"status\":\"Succeeded\",\"code\":\"OK\"}}")
            });

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "https://logic.test/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        TimelineRun run = await Timeline.Create()
            .SetVariable("logicRun", Var.Const(new LogicAppRunContext("OrderProcessor", "run-ctx")))
            .WaitForEvent(AzureExt.Event.LogicApp.RunCompleted("logic", Var.Ref<LogicAppRunContext>("logicRun")))
            .Build()
            .SetupRun(runtime.ServiceProvider)
            .RunAsync();

        run.EnsureRanToCompletion();
        Assert.Single(sender.Requests);
        Assert.Equal("https://logic.test/runtime/webhooks/workflow/api/management/workflows/OrderProcessor/runs/run-ctx?api-version=2022-03-01", sender.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task LogicAppRunCompleted_ReturnsTerminalDetails_WhenWorkflowFails()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"properties\":{\"status\":\"Failed\",\"code\":\"ActionFailed\"}}")
            });

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "https://logic.test/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        var evt = AzureExt.Event.LogicApp.RunCompleted("logic", Var.Const("run-500"), Var.Const("OrderProcessor"));

        LogicAppRunDetails? completed = await evt.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(completed);
        Assert.Equal(LogicAppRunStatus.Failed, completed!.Status);
        Assert.Equal("ActionFailed", completed.Code);
        Assert.Equal("run-500", completed.RunId);
    }

    [Fact]
        public async Task LogicAppRunCompleted_UsesRunsListFallback_WhenSingleRunEndpointIsMissing()
    {
        FakeHttpRequestSender sender = new(
                        new HttpResponseMessage(HttpStatusCode.NotFound),
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                                Content = new StringContent("""
                                {
                                    "value": [
                                        {
                                            "name": "run-123",
                                            "properties": {
                                                "status": "Succeeded",
                                                "code": "OK"
                                            }
                                        }
                                    ]
                                }
                                """)
                        });

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "https://logic.test/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        var evt = AzureExt.Event.LogicApp.RunCompleted("logic", Var.Const("run-123"), Var.Const("OrderProcessor"));

        LogicAppRunDetails? completed = await evt.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(completed);
        Assert.Equal(LogicAppRunStatus.Succeeded, completed!.Status);
        Assert.Equal(2, sender.Requests.Count);
        Assert.Equal("https://logic.test/runtime/webhooks/workflow/api/management/workflows/OrderProcessor/runs/run-123?api-version=2022-03-01", sender.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task LogicAppRunCompleted_UsesCancellation_WhenRunEndpointDoesNotRespond()
    {
        HangingHttpRequestSender sender = new();

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "https://logic.test/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new HangingHttpComponentFactory(sender) });
        });

        var evt = AzureExt.Event.LogicApp.RunCompleted("logic", Var.Const("run-nonresponsive"), Var.Const("OrderProcessor"));
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            evt.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, cancellation.Token));

        Assert.Single(sender.RequestUris);
        Assert.Equal("https://logic.test/runtime/webhooks/workflow/api/management/workflows/OrderProcessor/runs/run-nonresponsive?api-version=2022-03-01", sender.RequestUris[0]!.ToString());
    }


    [Fact]
    public async Task LogicAppRunCompleted_ThrowsHelpfulMessage_ForKnownStatelessWorkflow()
    {
        FakeHttpRequestSender sender = new(new HttpResponseMessage(HttpStatusCode.OK));

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
            services.AddSingleton<ILogicAppWorkflowMetadataProvider>(new StubLogicAppWorkflowMetadataProvider(("logic", "StatelessOrders"), LogicAppWorkflowMode.Stateless));
        });

        var evt = AzureExt.Event.LogicApp.RunCompleted("logic", Var.Const("run-stateless"), Var.Const("StatelessOrders"));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            evt.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None));

        Assert.Contains("stateless", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CallAndCapture", exception.Message, StringComparison.Ordinal);
        Assert.Empty(sender.Requests);
    }

    [Fact]
    public async Task LogicAppRunEvent_UsesConsumptionWorkflowResourceId()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"properties\":{\"status\":\"Succeeded\",\"code\":\"OK\"}}")
            });

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                HostingMode = LogicAppHostingMode.Consumption,
                Consumption = new LogicAppConsumptionConfig
                {
                    WorkflowResourceId = "/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
            services.AddSingleton<ILogicAppConsumptionManagementRequestAuthorizer>(new StubLogicAppConsumptionManagementRequestAuthorizer("test-token"));
        });

        var evt = AzureExt.Event.LogicApp.RunCompleted("logic", Var.Const("run-123"), Var.Const("OrderProcessor"));

        LogicAppRunDetails? completed = await evt.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(completed);
        Assert.Equal("https://management.azure.com/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor/runs/run-123?api-version=2019-05-01", sender.Requests[0].RequestUri!.ToString());
        Assert.Equal("Bearer", sender.Requests[0].Headers.Authorization!.Scheme);
        Assert.Equal(LogicAppRunStatus.Succeeded, completed!.Status);
    }

    [Fact]
    public async Task ManagedRemoteFunctionAppTrigger_UsesUnifiedFactorySender()
    {
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("managed")
        };
        FakeHttpRequestSender sender = new(response);
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<FunctionAppConfig>.Create("func", new FunctionAppConfig
            {
                BaseUrl = "https://example.test/api/",
                Code = "secret",
                AdminCode = null,
            }));
            services.AddSingleton(new FunctionAppTriggerConfig { DoPing = false });
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        ManagedRemoteFunctionAppTrigger trigger = new("func", Var.Const(new TriggerRouting("cleanup")));

        ManagedResult? result = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result!.StatusCode);
        Assert.Equal("managed", result.Body);
        Assert.Equal("https://example.test/api/admin/functions/cleanup", sender.Request!.RequestUri!.ToString());
        Assert.Equal("secret", sender.Request.Headers.GetValues("x-functions-key").Single());
        Assert.Equal("{}", await sender.Request.Content!.ReadAsStringAsync());
    }

    [Fact]
    public async Task FunctionAppIsLiveTrigger_UsesHostStatusEndpointAndConfiguredKey()
    {
        HttpResponseMessage response = new(HttpStatusCode.OK);
        FakeHttpRequestSender sender = new(response);
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<FunctionAppConfig>.Create("func", new FunctionAppConfig
            {
                BaseUrl = "https://example.test/api/",
                Code = "function-code",
                AdminCode = "admin-code",
            }));
            services.AddSingleton(new FunctionAppTriggerConfig { DoPing = false });
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        var trigger = AzureExt.Trigger.IsLive.FunctionApp("func");

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Equal("https://example.test/api/admin/host/status", sender.Request!.RequestUri!.ToString());
        Assert.Equal("admin-code", sender.Request.Headers.GetValues("x-functions-key").Single());
    }

    [Fact]
    public async Task LogicAppIsLiveTrigger_UsesHostStatusEndpointAndConfiguredKey()
    {
        HttpResponseMessage response = new(HttpStatusCode.OK);
        FakeHttpRequestSender sender = new(response);
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                Standard = new LogicAppStandardConfig
                {
                    BaseUrl = "https://logic.test/api/",
                    Code = "logic-code",
                    AdminCode = "logic-admin",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        var trigger = AzureExt.Trigger.IsLive.LogicApp("logic");

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Equal("https://logic.test/api/admin/host/status", sender.Request!.RequestUri!.ToString());
        Assert.Equal("logic-admin", sender.Request.Headers.GetValues("x-functions-key").Single());
    }

    [Fact]
    public async Task LogicAppIsLiveTrigger_UsesConsumptionWorkflowResourceId_WhenConfigured()
    {
        HttpResponseMessage response = new(HttpStatusCode.OK);
        FakeHttpRequestSender sender = new(response);
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                HostingMode = LogicAppHostingMode.Consumption,
                Consumption = new LogicAppConsumptionConfig
                {
                    WorkflowResourceId = "/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
            services.AddSingleton<ILogicAppConsumptionManagementRequestAuthorizer>(new StubLogicAppConsumptionManagementRequestAuthorizer("test-token"));
        });

        var trigger = AzureExt.Trigger.IsLive.LogicApp("logic");

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Equal("https://management.azure.com/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor/runs?api-version=2019-05-01", sender.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", sender.Request.Headers.Authorization!.Scheme);
    }

    [Fact]
    public async Task LogicAppIsLiveTrigger_ThrowsHelpfulMessage_WhenConsumptionManagementCapabilityIsMissing()
    {
        FakeHttpRequestSender sender = new(new HttpResponseMessage(HttpStatusCode.OK));

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                HostingMode = LogicAppHostingMode.Consumption,
                Consumption = new LogicAppConsumptionConfig
                {
                    WorkflowResourceId = "/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        var trigger = AzureExt.Trigger.IsLive.LogicApp("logic");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None));

        Assert.Contains("management capability", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(ILogicAppConsumptionManagementRequestAuthorizer), exception.Message, StringComparison.Ordinal);
        Assert.Null(sender.Request);
    }

    [Fact]
    public async Task LogicAppRunEvent_UsesConsumptionRunsListFallback_WhenRunEndpointReturnsNotFound()
    {
        FakeHttpRequestSender sender = new(
            new HttpResponseMessage(HttpStatusCode.NotFound),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":[{\"name\":\"run-123\",\"properties\":{\"status\":\"Succeeded\",\"code\":\"OK\"}}]}")
            });

        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<LogicAppConfig>.Create("logic", new LogicAppConfig
            {
                WorkflowName = "OrderProcessor",
                HostingMode = LogicAppHostingMode.Consumption,
                Consumption = new LogicAppConsumptionConfig
                {
                    WorkflowResourceId = "/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor",
                },
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
            services.AddSingleton<ILogicAppConsumptionManagementRequestAuthorizer>(new StubLogicAppConsumptionManagementRequestAuthorizer("test-token"));
        });

        var evt = AzureExt.Event.LogicApp.RunCompleted("logic", Var.Const("run-123"), Var.Const("OrderProcessor"));

        LogicAppRunDetails? completed = await evt.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(completed);
        Assert.Equal(2, sender.Requests.Count);
        Assert.Equal("https://management.azure.com/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor/runs/run-123?api-version=2019-05-01", sender.Requests[0].RequestUri!.ToString());
        Assert.Equal("https://management.azure.com/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.Logic/workflows/OrderProcessor/runs?api-version=2019-05-01", sender.Requests[1].RequestUri!.ToString());
        Assert.Equal(LogicAppRunStatus.Succeeded, completed!.Status);
    }

    [Fact]
    public async Task ServiceBusIsLiveTrigger_UsesAdministrationValidation()
    {
        FakeServiceBusAdministrationAdapter administration = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<ServiceBusConfig>.Create("bus", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = "orders",
                TopicName = null,
                SubscriptionName = null,
                RequiredSession = false,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { ServiceBusFactory = new FakeServiceBusComponentFactory(admin: administration) });
        });

        var trigger = AzureExt.Trigger.IsLive.ServiceBus("bus");

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(administration.ValidateCalled);
    }

    [Fact]
    public async Task ServiceBusIsLiveTrigger_AuthenticatedLevel_UsesNamespaceValidation()
    {
        FakeServiceBusAdministrationAdapter administration = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<ServiceBusConfig>.Create("bus", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = "orders",
                TopicName = null,
                SubscriptionName = null,
                RequiredSession = false,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { ServiceBusFactory = new FakeServiceBusComponentFactory(admin: administration) });
        });

        var trigger = AzureExt.Trigger.IsLive.ServiceBus("bus", AlivenessLevel.Authenticated);

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(administration.NamespaceValidateCalled);
        Assert.False(administration.ValidateCalled);
    }

    [Fact]
    public async Task BlobStorageIsLiveTrigger_UsesContainerValidation()
    {
        FakeBlobContainerAdapter container = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<StorageAccountConfig>.Create("storage", new StorageAccountConfig
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "blob",
                QueueContainerName = null,
                TableContainerName = "table",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { BlobFactory = new FakeBlobComponentFactory(container) });
        });

        var trigger = AzureExt.Trigger.IsLive.Blob("storage");

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(container.ValidateCalled);
    }

    [Fact]
    public async Task BlobStorageIsLiveTrigger_AuthenticatedLevel_UsesServiceValidation()
    {
        FakeBlobContainerAdapter container = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<StorageAccountConfig>.Create("storage", new StorageAccountConfig
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "blob",
                QueueContainerName = null,
                TableContainerName = "table",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { BlobFactory = new FakeBlobComponentFactory(container) });
        });

        var trigger = AzureExt.Trigger.IsLive.Blob("storage", AlivenessLevel.Authenticated);

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(container.ValidateServiceCalled);
        Assert.False(container.ValidateCalled);
    }

    [Fact]
    public async Task TableStorageIsLiveTrigger_UsesTableValidation()
    {
        FakeTableAdapter table = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<StorageAccountConfig>.Create("storage", new StorageAccountConfig
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "blob",
                QueueContainerName = null,
                TableContainerName = "table",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { TableFactory = new FakeTableComponentFactory(table) });
        });

        var trigger = AzureExt.Trigger.IsLive.Table("storage");

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(table.ValidateCalled);
        Assert.Equal("table", table.LastTableName);
    }

    [Fact]
    public async Task TableStorageIsLiveTrigger_AuthenticatedLevel_UsesServiceValidation()
    {
        FakeTableAdapter table = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<StorageAccountConfig>.Create("storage", new StorageAccountConfig
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "blob",
                QueueContainerName = null,
                TableContainerName = "table",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { TableFactory = new FakeTableComponentFactory(table) });
        });

        var trigger = AzureExt.Trigger.IsLive.Table("storage", AlivenessLevel.Authenticated);

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(table.ValidateServiceCalled);
        Assert.False(table.ValidateCalled);
        Assert.Equal("table", table.LastTableName);
    }

    [Fact]
    public async Task CosmosContainerIsLiveTrigger_UsesContainerValidation()
    {
        FakeCosmosContainerAdapter container = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<CosmosContainerDbConfig>.Create("cosmos", new CosmosContainerDbConfig
            {
                ConnectionString = "AccountEndpoint=https://cosmos.test/",
                DatabaseName = "db",
                ContainerName = "items",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { CosmosFactory = new FakeCosmosComponentFactory(container) });
        });

        var trigger = AzureExt.Trigger.IsLive.Cosmos("cosmos");

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(container.ValidateCalled);
    }

    [Fact]
    public async Task CosmosContainerIsLiveTrigger_AuthenticatedLevel_DoesNotRequireContainerValidation()
    {
        FakeCosmosContainerAdapter container = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<CosmosContainerDbConfig>.Create("cosmos", new CosmosContainerDbConfig
            {
                ConnectionString = "AccountEndpoint=https://cosmos.test/",
                DatabaseName = "db",
                ContainerName = "items",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { CosmosFactory = new FakeCosmosComponentFactory(container) });
        });

        var trigger = AzureExt.Trigger.IsLive.Cosmos("cosmos", AlivenessLevel.Authenticated);
        trigger.TimeOutOptions.TimeOut = Var.Const(TimeSpan.FromMilliseconds(150));

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(container.ValidateAccountCalled);
        Assert.False(container.ValidateCalled);
        Assert.False(container.ValidateReachabilityCalled);
    }

    [Fact]
    public async Task CosmosContainerIsLiveTrigger_ReachableLevel_UsesEndpointReachabilityValidation()
    {
        FakeCosmosContainerAdapter container = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<CosmosContainerDbConfig>.Create("cosmos", new CosmosContainerDbConfig
            {
                ConnectionString = "AccountEndpoint=https://cosmos.test/",
                DatabaseName = "db",
                ContainerName = "items",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { CosmosFactory = new FakeCosmosComponentFactory(container) });
        });

        var trigger = AzureExt.Trigger.IsLive.Cosmos("cosmos", AlivenessLevel.Reachable);
        trigger.TimeOutOptions.TimeOut = Var.Const(TimeSpan.FromMilliseconds(150));

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(container.ValidateReachabilityCalled);
        Assert.False(container.ValidateAccountCalled);
        Assert.False(container.ValidateCalled);
    }

    [Fact]
    public async Task CosmosContainerIsLiveTrigger_UsesConfiguredTimeoutForLocalEndpoint()
    {
        FakeCosmosContainerAdapter container = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<CosmosContainerDbConfig>.Create("cosmos", new CosmosContainerDbConfig
            {
                ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=emulator;",
                DatabaseName = "db",
                ContainerName = "items",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { CosmosFactory = new FakeCosmosComponentFactory(container) });
        });

        var trigger = AzureExt.Trigger.IsLive.Cosmos("cosmos");
        trigger.TimeOutOptions.TimeOut = Var.Const(TimeSpan.FromMilliseconds(150));

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(container.ValidateCalled);
    }

    [Fact]
    public async Task CosmosContainerIsLiveTrigger_TimesOutHungValidation()
    {
        FakeCosmosContainerAdapter container = new()
        {
            ValidateConnectionAsyncHandler = _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task,
        };
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<CosmosContainerDbConfig>.Create("cosmos", new CosmosContainerDbConfig
            {
                ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=emulator;",
                DatabaseName = "db",
                ContainerName = "items",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { CosmosFactory = new FakeCosmosComponentFactory(container) });
        });

        var trigger = AzureExt.Trigger.IsLive.Cosmos("cosmos");
        trigger.TimeOutOptions.TimeOut = Var.Const(TimeSpan.FromMilliseconds(50));

        TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None));

        Assert.Contains("WithTimeOut", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CosmosDbItemArtifactDescriber_UsesUnifiedFactoryContainer()
    {
        FakeCosmosContainerAdapter container = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<CosmosContainerDbConfig>.Create("cosmos", new CosmosContainerDbConfig
            {
                ConnectionString = "AccountEndpoint=https://cosmos.test/",
                DatabaseName = "db",
                ContainerName = "items",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { CosmosFactory = new FakeCosmosComponentFactory(container) });
        });

        CosmosDbItemArtifactDescriber<TestItem> describer = new();
        CosmosDbItemArtifactReference<TestItem> reference = new("cosmos");
        CosmosDbItemArtifactData<TestItem> data = new(new TestItem("42", "tenant-1", "Ada"));

        await describer.Setup(runtime.ServiceProvider, data, reference, runtime.VariableStore, runtime.Logger);
        await describer.Deconstruct(runtime.ServiceProvider, reference, runtime.VariableStore, runtime.Logger);

        Assert.Equal("42", reference.GetId(runtime.VariableStore));
        Assert.Equal(new PartitionKey("tenant-1"), reference.GetPartitionKey(runtime.VariableStore));
        Assert.True(container.EnsureContainerExistsCalled);
        Assert.Equal("42", container.UpsertedItem!.id);
        Assert.Equal(new PartitionKey("tenant-1"), container.UpsertedPartitionKey);
        Assert.Equal("42", container.DeletedId);
        Assert.Equal(new PartitionKey("tenant-1"), container.DeletedPartitionKey);
    }

    [Fact]
    public async Task CosmosDbItemArtifactReference_ResolveToData_UsesUnifiedFactoryContainer()
    {
        FakeCosmosContainerAdapter container = new()
        {
            ReadResponse = new CosmosReadResponse(true, new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":\"42\",\"partitionKey\":\"tenant-1\",\"name\":\"Ada\"}")))
        };
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<CosmosContainerDbConfig>.Create("cosmos", new CosmosContainerDbConfig
            {
                ConnectionString = "AccountEndpoint=https://cosmos.test/",
                DatabaseName = "db",
                ContainerName = "items",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { CosmosFactory = new FakeCosmosComponentFactory(container) });
        });

        CosmosDbItemArtifactReference<TestItem> reference = new("cosmos", Var.Const(new PartitionKey("tenant-1")), Var.Const("42"));

        ArtifactResolveResult<CosmosDbItemArtifactDescriber<TestItem>, CosmosDbItemArtifactData<TestItem>, CosmosDbItemArtifactReference<TestItem>> result =
            await reference.ResolveToDataAsync(runtime.ServiceProvider, ArtifactVersionIdentifier.Default, runtime.VariableStore, runtime.Logger);

        Assert.True(result.Found);
        Assert.Equal("Ada", result.Data!.Item.name);
        Assert.Equal("42", container.ReadId);
        Assert.Equal(new PartitionKey("tenant-1"), container.ReadPartitionKey);
    }

    [Fact]
    public async Task CosmosDbItemArtifactQueryFinder_FindMultiAsync_UsesUnifiedFactoryContainer()
    {
        FakeCosmosContainerAdapter container = new();
        container.QueryItems.Add(new TestItem("1", "p1", "Ada"));
        container.QueryItems.Add(new TestItem("2", "p2", "Grace"));
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<CosmosContainerDbConfig>.Create("cosmos", new CosmosContainerDbConfig
            {
                ConnectionString = "AccountEndpoint=https://cosmos.test/",
                DatabaseName = "db",
                ContainerName = "items",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { CosmosFactory = new FakeCosmosComponentFactory(container) });
        });

        CosmosDbItemArtifactQueryFinder<TestItem> finder = new("cosmos", Var.Const(new QueryDefinition("SELECT * FROM c")));

        ArtifactFinderResultMulti results = await finder.FindMultiAsync(runtime.ServiceProvider, runtime.VariableStore, runtime.Logger, CancellationToken.None);

        Assert.Equal(2, results.ArtifactReferences.Length);
        Assert.Equal("SELECT * FROM c", container.LastQuery?.QueryText);
    }

    [Fact]
    public void CosmosModelSchemaResolver_UsesSameSerializedPartitionKeyForValueAndPath()
    {
        JsonNamedCosmosItem item = new("42", "tenant-json");

        string id = CosmosModelSchemaResolver.ResolveId(item);
        PartitionKey partitionKey = CosmosModelSchemaResolver.ResolvePartitionKey(item);
        string partitionKeyPath = CosmosModelSchemaResolver.ResolvePartitionKeyPath<JsonNamedCosmosItem>();

        Assert.Equal("42", id);
        Assert.Equal(new PartitionKey("tenant-json"), partitionKey);
        Assert.Equal("/partitionKey", partitionKeyPath);
    }

    [Fact]
    public async Task StorageAccountBlobArtifactDescriber_UsesUnifiedFactoryContainer()
    {
        FakeBlobContainerAdapter container = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<StorageAccountConfig>.Create("storage", new StorageAccountConfig
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "blob",
                QueueContainerName = "queue",
                TableContainerName = "table",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { BlobFactory = new FakeBlobComponentFactory(container) });
        });

        StorageAccountBlobArtifactDescriber describer = new();
        StorageAccountBlobArtifactReference reference = new("storage", Var.Const("samples/a.txt"));
        StorageAccountBlobArtifactData data = new(Encoding.UTF8.GetBytes("hello"), new Dictionary<string, string> { ["env"] = "test" });

        await describer.Setup(runtime.ServiceProvider, data, reference, runtime.VariableStore, runtime.Logger);
        await describer.Deconstruct(runtime.ServiceProvider, reference, runtime.VariableStore, runtime.Logger);

        Assert.True(container.CreateCalled);
        Assert.Equal("samples/a.txt", container.UploadedPath);
        Assert.Equal("hello", Encoding.UTF8.GetString(container.UploadedData!));
        Assert.Equal("test", container.UploadedMetadata!["env"]);
        Assert.Equal("samples/a.txt", container.DeletedPath);
    }

    [Fact]
    public async Task StorageAccountBlobArtifactReference_ResolveToData_UsesUnifiedFactoryContainer()
    {
        FakeBlobContainerAdapter container = new()
        {
            ReadResponse = new BlobReadResponse(true, Encoding.UTF8.GetBytes("hello"), new Dictionary<string, string> { ["env"] = "test" })
        };
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<StorageAccountConfig>.Create("storage", new StorageAccountConfig
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "blob",
                QueueContainerName = "queue",
                TableContainerName = "table",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { BlobFactory = new FakeBlobComponentFactory(container) });
        });

        StorageAccountBlobArtifactReference reference = new("storage", Var.Const("samples/a.txt"));

        ArtifactResolveResult<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference> result =
            await reference.ResolveToDataAsync(runtime.ServiceProvider, ArtifactVersionIdentifier.Default, runtime.VariableStore, runtime.Logger);

        Assert.True(result.Found);
        Assert.Equal("hello", Encoding.UTF8.GetString(result.Data!.Data));
        Assert.Equal("test", result.Data.MetaData["env"]);
        Assert.Equal("samples/a.txt", container.ReadPath);
    }

    [Fact]
    public async Task TableStorageEntityArtifactDescriber_UsesUnifiedFactoryTable()
    {
        FakeTableAdapter table = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<StorageAccountConfig>.Create("storage", new StorageAccountConfig
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "blob",
                QueueContainerName = "queue",
                TableContainerName = "table",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { TableFactory = new FakeTableComponentFactory(table) });
        });

        TableStorageEntityArtifactDescriber<TestTableEntity> describer = new();
        TableStorageEntityArtifactReference<TestTableEntity> reference = new("storage", Var.Const("orders"), Var.Const("partition"), Var.Const("row"));
        TestTableEntity entity = new() { PartitionKey = "partition", RowKey = "row", Name = "Ada" };
        TableStorageEntityArtifactData<TestTableEntity> data = new(entity);

        await describer.Setup(runtime.ServiceProvider, data, reference, runtime.VariableStore, runtime.Logger);
        await describer.Deconstruct(runtime.ServiceProvider, reference, runtime.VariableStore, runtime.Logger);

        Assert.Equal("orders", table.LastTableName);
        Assert.True(table.CreateCalled);
        Assert.Same(entity, table.UpsertedEntity);
        Assert.Equal(("partition", "row"), table.DeletedEntity);
    }

    [Fact]
    public async Task TableStorageEntityArtifactReference_ResolveToData_UsesUnifiedFactoryTable()
    {
        FakeTableAdapter table = new()
        {
            ReadEntity = new TestTableEntity { PartitionKey = "partition", RowKey = "row", Name = "Grace" }
        };
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<StorageAccountConfig>.Create("storage", new StorageAccountConfig
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "blob",
                QueueContainerName = "queue",
                TableContainerName = "table",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { TableFactory = new FakeTableComponentFactory(table) });
        });

        TableStorageEntityArtifactReference<TestTableEntity> reference = new("storage", Var.Const("orders"), Var.Const("partition"), Var.Const("row"));

        ArtifactResolveResult<TableStorageEntityArtifactDescriber<TestTableEntity>, TableStorageEntityArtifactData<TestTableEntity>, TableStorageEntityArtifactReference<TestTableEntity>> result =
            await reference.ResolveToDataAsync(runtime.ServiceProvider, ArtifactVersionIdentifier.Default, runtime.VariableStore, runtime.Logger);

        Assert.True(result.Found);
        Assert.Equal("Grace", result.Data!.Entity.Name);
        Assert.Equal(("partition", "row"), table.ReadKeys);
    }

    [Fact]
    public async Task TableStorageEntityArtifactQueryFinder_FindMultiAsync_UsesUnifiedFactoryTable()
    {
        FakeTableAdapter table = new();
        table.QueryResults.Add(new TestTableEntity { PartitionKey = "p1", RowKey = "r1", Name = "Ada" });
        table.QueryResults.Add(new TestTableEntity { PartitionKey = "p2", RowKey = "r2", Name = "Grace" });
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<StorageAccountConfig>.Create("storage", new StorageAccountConfig
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "blob",
                QueueContainerName = "queue",
                TableContainerName = "table",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { TableFactory = new FakeTableComponentFactory(table) });
        });

        TableStorageEntityArtifactQueryFinder<TestTableEntity> finder = new("storage", Var.Const("orders"), "PartitionKey ne ''");

        ArtifactFinderResultMulti results = await finder.FindMultiAsync(runtime.ServiceProvider, runtime.VariableStore, runtime.Logger, CancellationToken.None);

        Assert.Equal(2, results.ArtifactReferences.Length);
        Assert.Equal("PartitionKey ne ''", table.LastFilter);
        Assert.Equal("orders", table.LastTableName);
    }

    [Fact]
    public async Task ServiceBusCreateTempSubscriptionStep_UsesUnifiedFactoryAdministration()
    {
        FakeServiceBusAdministrationAdapter administration = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<ServiceBusConfig>.Create("topic", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = null,
                TopicName = "orders-topic",
                SubscriptionName = "default-sub",
                RequiredSession = true,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { ServiceBusFactory = new FakeServiceBusComponentFactory(admin: administration) });
        });

        ServiceBusCreateTempSubscriptionStep step = new("topic", "tmp-123", Var.Const("m1"), Var.Const("c1"), TimeSpan.FromMinutes(5));

        await step.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Equal("orders-topic", administration.CreatedOptions!.TopicName);
        Assert.Equal("tmp-123", administration.CreatedOptions.SubscriptionName);
        CorrelationRuleFilter filter = Assert.IsType<CorrelationRuleFilter>(administration.CreatedRuleOptions!.Filter);
        Assert.Equal("m1", filter.MessageId);
        Assert.Equal("c1", filter.CorrelationId);
    }

    [Fact]
    public async Task ServiceBusDeleteTempSubscriptionStep_UsesUnifiedFactoryAdministration()
    {
        FakeServiceBusAdministrationAdapter administration = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<ServiceBusConfig>.Create("topic", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = null,
                TopicName = "orders-topic",
                SubscriptionName = "default-sub",
                RequiredSession = false,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { ServiceBusFactory = new FakeServiceBusComponentFactory(admin: administration) });
        });

        ServiceBusDeleteTempSubscriptionStep step = new("topic", "tmp-123");

        await step.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Equal(("orders-topic", "tmp-123"), administration.DeletedSubscription);
    }

    [Fact]
    public async Task ServiceBusProcessEvent_UsesUnifiedFactoryPumpAndHonorsPredicate()
    {
        ServiceBusReceivedMessage receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "m1", correlationId: "c1", body: new BinaryData("payload"));
        FakeServiceBusMessagePump pump = new() { MessageToReturn = receivedMessage };
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<ServiceBusConfig>.Create("topic", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = null,
                TopicName = "orders-topic",
                SubscriptionName = "default-sub",
                RequiredSession = false,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { ServiceBusFactory = new FakeServiceBusComponentFactory(pump: pump) });
        });

        ServiceBusProcessEvent step = AzureExt.Event.ServiceBus.MessageReceived(
            "topic",
            messageId: Var.Const("m1"),
            correlationId: Var.Const("c1"),
            predicate: Var.Const<Func<ServiceBusReceivedMessage, bool>>(message => message.MessageId == "m1"),
            completeMessage: Var.Const(true));

        var result = await step.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Same(receivedMessage, result!.Message);
        Assert.Equal("m1", pump.LastRequest!.Value.MessageId);
        Assert.Equal("c1", pump.LastRequest.Value.CorrelationId);
        Assert.True(pump.LastRequest.Value.CompleteMessage);
        Assert.Equal("default-sub", pump.SubscriptionName);
        Assert.True(pump.LastRequest.Value.Predicate!(receivedMessage));
    }

    [Fact]
    public async Task ServiceBusProcessEvent_AllowsQueueConfigWithoutSubscription_NonSession()
    {
        ServiceBusReceivedMessage receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "q1", body: new BinaryData("payload"));
        FakeServiceBusMessagePump pump = new() { MessageToReturn = receivedMessage };
        FakeServiceBusSender sender = new();
        FakeServiceBusComponentFactory serviceBusFactory = new(sender: sender, pump: pump);
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<ServiceBusConfig>.Create("queue", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = "orders-queue",
                TopicName = null,
                SubscriptionName = null,
                RequiredSession = false,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { ServiceBusFactory = serviceBusFactory });
        });

        ServiceBusMessage sendMessage = new("payload") { MessageId = "q1" };
        ServiceBusSendTrigger sendTrigger = AzureExt.Trigger.ServiceBus.Send("queue", Var.Const(sendMessage));
        ServiceBusProcessEvent receiveEvent = AzureExt.Event.ServiceBus.MessageReceived("queue", messageId: Var.Const("q1"));

        await sendTrigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);
        var result = await receiveEvent.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Same(sendMessage, sender.MessageSent);
        Assert.NotNull(result);
        Assert.Same(receivedMessage, result!.Message);
        Assert.Null(pump.SubscriptionName);
        Assert.Equal("q1", pump.LastRequest!.Value.MessageId);
    }

    [Fact]
    public async Task ServiceBusProcessEvent_AllowsQueueConfigWithoutSubscription_Session()
    {
        ServiceBusReceivedMessage receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "q2", body: new BinaryData("payload"));
        FakeServiceBusMessagePump pump = new() { MessageToReturn = receivedMessage };
        FakeServiceBusSender sender = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<ServiceBusConfig>.Create("queue", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = "orders-session-queue",
                TopicName = null,
                SubscriptionName = null,
                RequiredSession = true,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { ServiceBusFactory = new FakeServiceBusComponentFactory(sender: sender, pump: pump) });
        });

        ServiceBusMessage sendMessage = new("payload") { MessageId = "q2" };
        ServiceBusSendTrigger sendTrigger = AzureExt.Trigger.ServiceBus.Send("queue", Var.Const(sendMessage));
        ServiceBusProcessEvent receiveEvent = AzureExt.Event.ServiceBus.MessageReceived("queue", messageId: Var.Const("q2"));

        await sendTrigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);
        var result = await receiveEvent.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Same(sendMessage, sender.MessageSent);
        Assert.False(string.IsNullOrWhiteSpace(sendMessage.SessionId));
        Assert.NotNull(result);
        Assert.Same(receivedMessage, result!.Message);
        Assert.Null(pump.SubscriptionName);
        Assert.Equal("q2", pump.LastRequest!.Value.MessageId);
    }

    [Fact]
    public async Task ServiceBusProcessEvent_UsesTemporarySubscriptionForTopicReceive()
    {
        ServiceBusReceivedMessage receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "t1", body: new BinaryData("payload"));
        FakeServiceBusMessagePump pump = new() { MessageToReturn = receivedMessage };
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<ServiceBusConfig>.Create("topic", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = null,
                TopicName = "orders-topic",
                SubscriptionName = null,
                RequiredSession = false,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { ServiceBusFactory = new FakeServiceBusComponentFactory(pump: pump) });
        });

        ServiceBusProcessEvent receiveEvent = AzureExt.Event.ServiceBus.MessageReceived("topic", messageId: Var.Const("t1"), createTempSubscription: true);

        StepGeneric? preStep = ((IHasPreStep)receiveEvent).CreatePreStep(runtime.VariableStore);
        StepGeneric? cleanupStep = ((IHasCleanupStep)receiveEvent).CreateCleanupStep(runtime.VariableStore);
        var result = await receiveEvent.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.IsType<ServiceBusCreateTempSubscriptionStep>(preStep);
        Assert.IsType<ServiceBusDeleteTempSubscriptionStep>(cleanupStep);
        Assert.NotNull(result);
        Assert.Same(receivedMessage, result!.Message);
        Assert.NotNull(pump.SubscriptionName);
        Assert.StartsWith("tmp-", pump.SubscriptionName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServiceBusProcessEvent_ThrowsWhenTopicConfigIsMissingSubscription()
    {
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(ConfigStore<ServiceBusConfig>.Create("topic", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = null,
                TopicName = "orders-topic",
                SubscriptionName = null,
                RequiredSession = false,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory());
        });

        ServiceBusProcessEvent receiveEvent = AzureExt.Event.ServiceBus.MessageReceived("topic", messageId: Var.Const("missing-sub"));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            receiveEvent.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None));

        Assert.Contains("createTempSubscription", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static void AssertIdentifierRoundTrip<TIdentifier>(string value, Func<string, TIdentifier> create, Func<TIdentifier, string> toString)
    {
        TIdentifier identifier = create(value);

        Assert.Equal(value, toString(identifier));
        Assert.Equal(value, identifier?.ToString());
    }

    private sealed class RuntimeContext
    {
        public IServiceProvider ServiceProvider { get; }
        public ScopedLogger Logger { get; }
        public VariableStore VariableStore { get; }
        public ArtifactStore ArtifactStore { get; }

        private RuntimeContext(Action<IServiceCollection>? configureServices)
        {
            ServiceCollection services = new();
            configureServices?.Invoke(services);
            ServiceProvider = services.BuildServiceProvider();

            Logger = (ScopedLogger)Activator.CreateInstance(
                typeof(ScopedLogger),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args: new object?[] { null },
                culture: null)!;

            Type coreAssemblyType = typeof(VariableStore);
            Type debuggingRunSessionType = coreAssemblyType.Assembly.GetType("TestFramework.Core.Debugger.DebuggingRunSession", throwOnError: true)!;
            Type emptyRunDebuggerType = coreAssemblyType.Assembly.GetType("TestFramework.Core.Debugger.EmptyRunDebugger", throwOnError: true)!;
            object emptyRunDebugger = emptyRunDebuggerType.GetMethod("CreateNew", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!.Invoke(null, null)!;
            object debuggingSession = debuggingRunSessionType
                .GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Single()
                .Invoke([emptyRunDebugger]);

            VariableStore = (VariableStore)Activator.CreateInstance(
                typeof(VariableStore),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args: new object?[] { Logger, debuggingSession },
                culture: null)!;

            ArtifactStore = (ArtifactStore)Activator.CreateInstance(
                typeof(ArtifactStore),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args: new object?[] { Logger, debuggingSession },
                culture: null)!;
        }

        public static RuntimeContext Create(Action<IServiceCollection>? configureServices = null) => new(configureServices);
    }

    private sealed class FakeAzureComponentFactory : IAzureComponentFactory
    {
        public ICosmosComponentFactory Cosmos { get; init; } = new FakeCosmosComponentFactory(new FakeCosmosContainerAdapter());
        public IBlobComponentFactory Blob { get; init; } = new FakeBlobComponentFactory(new FakeBlobContainerAdapter());
        public ITableComponentFactory Table { get; init; } = new FakeTableComponentFactory(new FakeTableAdapter());
        public IServiceBusComponentFactory ServiceBus { get; init; } = new FakeServiceBusComponentFactory();
        public IHttpComponentFactory Http { get; init; } = new FakeHttpComponentFactory(new FakeHttpRequestSender(new HttpResponseMessage(HttpStatusCode.OK)));

        public ICosmosComponentFactory CosmosFactory { init => Cosmos = value; }
        public IBlobComponentFactory BlobFactory { init => Blob = value; }
        public ITableComponentFactory TableFactory { init => Table = value; }
        public IServiceBusComponentFactory ServiceBusFactory { init => ServiceBus = value; }
        public IHttpComponentFactory HttpFactory { init => Http = value; }
    }

    private sealed class FakeCosmosComponentFactory(FakeCosmosContainerAdapter container) : ICosmosComponentFactory
    {
        public ICosmosContainerAdapter CreateContainer(CosmosContainerDbConfig config) => container;
    }

    private sealed class FakeCosmosContainerAdapter : ICosmosContainerAdapter
    {
        public bool ValidateReachabilityCalled { get; private set; }
        public bool ValidateAccountCalled { get; private set; }
        public bool ValidateCalled { get; private set; }
        public bool EnsureContainerExistsCalled { get; private set; }
        public Func<CancellationToken, Task>? ValidateReachabilityAsyncHandler { get; set; }
        public Func<CancellationToken, Task>? ValidateAccountConnectionAsyncHandler { get; set; }
        public Func<CancellationToken, Task>? ValidateConnectionAsyncHandler { get; set; }
        public TestItem? UpsertedItem { get; private set; }
        public PartitionKey UpsertedPartitionKey { get; private set; } = PartitionKey.Null;
        public string? DeletedId { get; private set; }
        public PartitionKey DeletedPartitionKey { get; private set; } = PartitionKey.Null;
        public string? ReadId { get; private set; }
        public PartitionKey ReadPartitionKey { get; private set; } = PartitionKey.Null;
        public CosmosReadResponse ReadResponse { get; set; } = new(false, null);
        public QueryDefinition? LastQuery { get; private set; }
        public List<object> QueryItems { get; } = [];

        public Task ValidateAccountReachabilityAsync(CancellationToken cancellationToken)
        {
            ValidateReachabilityCalled = true;
            return ValidateReachabilityAsyncHandler?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public Task ValidateAccountConnectionAsync(CancellationToken cancellationToken)
        {
            ValidateAccountCalled = true;
            return ValidateAccountConnectionAsyncHandler?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            ValidateCalled = true;
            return ValidateConnectionAsyncHandler?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public Task EnsureContainerExistsAsync<TItem>(TItem item)
        {
            EnsureContainerExistsCalled = true;
            return Task.CompletedTask;
        }

        public Task DeleteItemAsync<TItem>(string id, PartitionKey partitionKey)
        {
            DeletedId = id;
            DeletedPartitionKey = partitionKey;
            return Task.CompletedTask;
        }

        public Task UpsertItemAsync<TItem>(TItem item, PartitionKey partitionKey)
        {
            UpsertedItem = item as TestItem;
            UpsertedPartitionKey = partitionKey;
            return Task.CompletedTask;
        }

        public Task<CosmosReadResponse> ReadItemAsync(string id, PartitionKey partitionKey)
        {
            ReadId = id;
            ReadPartitionKey = partitionKey;
            return Task.FromResult(ReadResponse);
        }

        public async IAsyncEnumerable<TItem> QueryItemsAsync<TItem>(QueryDefinition query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastQuery = query;
            foreach (object item in QueryItems)
            {
                yield return (TItem)item;
                await Task.Yield();
            }
        }
    }

    private sealed class FakeBlobComponentFactory(FakeBlobContainerAdapter container) : IBlobComponentFactory
    {
        public IBlobContainerAdapter CreateContainer(StorageAccountConfig config) => container;
    }

    private sealed class FakeBlobContainerAdapter : IBlobContainerAdapter
    {
        public bool CreateCalled { get; private set; }
        public bool ValidateServiceCalled { get; private set; }
        public bool ValidateCalled { get; private set; }
        public string? UploadedPath { get; private set; }
        public byte[]? UploadedData { get; private set; }
        public IReadOnlyDictionary<string, string>? UploadedMetadata { get; private set; }
        public string? DeletedPath { get; private set; }
        public string? ReadPath { get; private set; }
        public BlobReadResponse ReadResponse { get; set; } = new(false, null, null);

        public Task ValidateServiceConnectionAsync(CancellationToken cancellationToken)
        {
            ValidateServiceCalled = true;
            return Task.CompletedTask;
        }

        public Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            ValidateCalled = true;
            return Task.CompletedTask;
        }

        public Task CreateIfNotExistsAsync()
        {
            CreateCalled = true;
            return Task.CompletedTask;
        }

        public Task DeleteBlobAsync(string path)
        {
            DeletedPath = path;
            return Task.CompletedTask;
        }

        public Task UploadBlobAsync(string path, byte[] data, IReadOnlyDictionary<string, string> metadata)
        {
            UploadedPath = path;
            UploadedData = data;
            UploadedMetadata = metadata;
            return Task.CompletedTask;
        }

        public Task<BlobReadResponse> ReadBlobAsync(string path)
        {
            ReadPath = path;
            return Task.FromResult(ReadResponse);
        }
    }

    private sealed class FakeTableComponentFactory(FakeTableAdapter table) : ITableComponentFactory
    {
        public ITableAdapter CreateTable(StorageAccountConfig config, string tableName)
        {
            table.LastTableName = tableName;
            return table;
        }
    }

    private sealed class FakeTableAdapter : ITableAdapter
    {
        public bool CreateCalled { get; private set; }
        public bool ValidateServiceCalled { get; private set; }
        public bool ValidateCalled { get; private set; }
        public string? LastTableName { get; set; }
        public object? UpsertedEntity { get; private set; }
        public (string PartitionKey, string RowKey)? DeletedEntity { get; private set; }
        public (string PartitionKey, string RowKey)? ReadKeys { get; private set; }
        public object? ReadEntity { get; set; }
        public string? LastFilter { get; private set; }
        public List<object> QueryResults { get; } = [];

        public Task ValidateServiceConnectionAsync(CancellationToken cancellationToken)
        {
            ValidateServiceCalled = true;
            return Task.CompletedTask;
        }

        public Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            ValidateCalled = true;
            return Task.CompletedTask;
        }

        public Task CreateIfNotExistsAsync()
        {
            CreateCalled = true;
            return Task.CompletedTask;
        }

        public Task UpsertEntityAsync<T>(T entity) where T : class, ITableEntity
        {
            UpsertedEntity = entity;
            return Task.CompletedTask;
        }

        public Task DeleteEntityAsync(string partitionKey, string rowKey)
        {
            DeletedEntity = (partitionKey, rowKey);
            return Task.CompletedTask;
        }

        public Task<TableReadResponse<T>> GetEntityAsync<T>(string partitionKey, string rowKey) where T : class, ITableEntity
        {
            ReadKeys = (partitionKey, rowKey);
            return Task.FromResult(ReadEntity is T entity
                ? new TableReadResponse<T>(true, entity)
                : new TableReadResponse<T>(false, null));
        }

        public async IAsyncEnumerable<T> QueryEntitiesAsync<T>(string filter, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken) where T : class, ITableEntity
        {
            LastFilter = filter;
            foreach (object result in QueryResults)
            {
                yield return (T)result;
                await Task.Yield();
            }
        }
    }

    private sealed class FakeServiceBusComponentFactory(
        FakeServiceBusSender? sender = null,
        FakeServiceBusMessagePump? pump = null,
        FakeServiceBusAdministrationAdapter? admin = null) : IServiceBusComponentFactory
    {
        private readonly FakeServiceBusSender _sender = sender ?? new FakeServiceBusSender();
        private readonly FakeServiceBusMessagePump _pump = pump ?? new FakeServiceBusMessagePump();
        private readonly FakeServiceBusAdministrationAdapter _admin = admin ?? new FakeServiceBusAdministrationAdapter();

        public FakeServiceBusSender Sender => _sender;

        public IServiceBusSenderAdapter CreateSender(ServiceBusConfig config) => _sender;

        public IServiceBusMessagePump CreateMessagePump(ServiceBusConfig config, string? subscriptionName)
        {
            _pump.SubscriptionName = subscriptionName;
            return _pump;
        }

        public IServiceBusAdministrationAdapter CreateAdministration(ServiceBusConfig config) => _admin;
    }

    private sealed class FakeServiceBusSender : IServiceBusSenderAdapter
    {
        public ServiceBusMessage? MessageSent { get; private set; }

        public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken)
        {
            MessageSent = message;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeServiceBusMessagePump : IServiceBusMessagePump
    {
        public ServiceBusReceiveRequest? LastRequest { get; private set; }
        public string? SubscriptionName { get; set; }
        public ServiceBusReceivedMessage MessageToReturn { get; set; } = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "default", body: new BinaryData("payload"));

        public Task<ServiceBusReceivedMessage> ReceiveMessageAsync(ServiceBusReceiveRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(MessageToReturn);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeServiceBusAdministrationAdapter : IServiceBusAdministrationAdapter
    {
        public bool NamespaceValidateCalled { get; private set; }
        public bool ValidateCalled { get; private set; }
        public CreateSubscriptionOptions? CreatedOptions { get; private set; }
        public CreateRuleOptions? CreatedRuleOptions { get; private set; }
        public (string TopicName, string SubscriptionName)? DeletedSubscription { get; private set; }

        public Task ValidateNamespaceConnectionAsync(CancellationToken cancellationToken)
        {
            NamespaceValidateCalled = true;
            return Task.CompletedTask;
        }

        public Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            ValidateCalled = true;
            return Task.CompletedTask;
        }

        public Task CreateSubscriptionAsync(CreateSubscriptionOptions options, CreateRuleOptions? ruleOptions, CancellationToken cancellationToken)
        {
            CreatedOptions = options;
            CreatedRuleOptions = ruleOptions;
            return Task.CompletedTask;
        }

        public Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken)
        {
            DeletedSubscription = (topicName, subscriptionName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHttpComponentFactory(FakeHttpRequestSender sender) : IHttpComponentFactory
    {
        public IHttpRequestSender CreateSender() => sender;
    }

    private sealed class StubLogicAppWorkflowMetadataProvider((string Identifier, string WorkflowName) match, LogicAppWorkflowMode mode) : ILogicAppWorkflowMetadataProvider
    {
        public bool TryGetWorkflowMode(LogicAppIdentifier identifier, string workflowName, out LogicAppWorkflowMode resolvedMode)
        {
            if (string.Equals(identifier.Identifier, match.Identifier, StringComparison.Ordinal)
                && string.Equals(workflowName, match.WorkflowName, StringComparison.Ordinal))
            {
                resolvedMode = mode;
                return true;
            }

            resolvedMode = LogicAppWorkflowMode.Unknown;
            return false;
        }
    }

    private sealed class StubLogicAppConsumptionManagementRequestAuthorizer(string token) : ILogicAppConsumptionManagementRequestAuthorizer
    {
        public Task AuthorizeAsync(LogicAppIdentifier identifier, LogicAppConfig config, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return Task.CompletedTask;
        }
    }

    private sealed class HangingHttpComponentFactory(HangingHttpRequestSender sender) : IHttpComponentFactory
    {
        public IHttpRequestSender CreateSender() => sender;
    }

    private sealed class FakeHttpRequestSender(params HttpResponseMessage[] responses) : IHttpRequestSender
    {
        private int _nextResponseIndex;

        public List<HttpRequestMessage> Requests { get; } = [];
        public IReadOnlyList<HttpResponseMessage> Responses => responses;

        public HttpRequestMessage? Request => Requests.LastOrDefault();

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await CloneRequestAsync(request, cancellationToken));
            HttpResponseMessage response = _nextResponseIndex < responses.Length
                ? responses[_nextResponseIndex++]
                : responses.Last();
            return response;
        }

        private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpRequestMessage clone = new(request.Method, request.RequestUri);

            foreach (var header in request.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

            if (request.Content is not null)
            {
                byte[] contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                ByteArrayContent contentClone = new(contentBytes);
                foreach (var header in request.Content.Headers)
                    contentClone.Headers.TryAddWithoutValidation(header.Key, header.Value);

                clone.Content = contentClone;
            }

            return clone;
        }
    }

    private sealed class HangingHttpRequestSender : IHttpRequestSender
    {
        public List<Uri?> RequestUris { get; } = [];

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The hanging sender should be canceled before completing.");
        }
    }

    private sealed record TestItem(string id, string partitionKey, string name);
    private sealed record JsonNamedCosmosItem([property: JsonProperty("id")] string Identifier, [property: JsonProperty("partitionKey")] string TenantKey);

    private sealed class TestTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public global::Azure.ETag ETag { get; set; }
        public string? Name { get; set; }
    }

    private sealed class StubConfigProvider : IConfigProvider
    {
        public string[] LoadAllLogicAppIdentifier(IConfiguration configuration) => new[] { "logic-app" };
        public LogicAppConfig LoadLogicAppConfig(IConfiguration configuration, string identifier) => new() { WorkflowName = "OrderProcessor", HostingMode = LogicAppHostingMode.Standard, Standard = new LogicAppStandardConfig { BaseUrl = "https://logic.test", Code = "logic-code", AdminCode = "logic-admin" } };
        public string[] LoadAllFunctionAppIdentifier(IConfiguration configuration) => new[] { "func" };
        public FunctionAppConfig LoadFunctionAppConfig(IConfiguration configuration, string identifier) => new() { BaseUrl = "https://functions.test", Code = "function-code", AdminCode = "admin" };
        public string[] LoadAllStorageAccountIdentifier(IConfiguration configuration) => new[] { "storage" };
        public StorageAccountConfig LoadStorageAccountConfig(IConfiguration configuration, string identifier) => new() { ConnectionString = "UseDevelopmentStorage=true", BlobContainerName = "blob", QueueContainerName = "queue", TableContainerName = "table" };
        public string[] LoadAllCosmosDbIdentifier(IConfiguration configuration) => new[] { "cosmos" };
        public CosmosContainerDbConfig LoadCosmosDbConfig(IConfiguration configuration, string identifier) => new() { ConnectionString = "AccountEndpoint=https://cosmos.test/", DatabaseName = "db", ContainerName = "items" };
        public string[] LoadAllServiceBusIdentifier(IConfiguration configuration) => new[] { "bus" };
        public ServiceBusConfig LoadServiceBusConfig(IConfiguration configuration, string identifier) => new() { ConnectionString = "Endpoint=sb://bus/", QueueName = "queue", TopicName = null, SubscriptionName = null, RequiredSession = false };
        public string[] LoadAllSqlDatabaseIdentifier(IConfiguration configuration) => new[] { "sql" };
        public SqlDatabaseConfig LoadSqlDatabaseConfig(IConfiguration configuration, string identifier) => new() { ConnectionString = "Server=(localdb)\\MSSQLLocalDB;", DatabaseName = "main", ContextType = null };
    }
}
