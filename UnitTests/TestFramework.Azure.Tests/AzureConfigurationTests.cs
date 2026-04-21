using Azure.Data.Tables;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
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
using TestFramework.Azure.Runtime;
using TestFramework.Azure.ServiceBus;
using TestFramework.Azure.StorageAccount.Blob;
using TestFramework.Azure.StorageAccount.Table;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Tests;

public class AzureConfigurationTests
{
    [Fact]
    public void IdentifierRecords_RoundTripAsStrings()
    {
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
            ["ServiceBus:orders:ConnectionString"] = "Endpoint=sb://orders/",
            ["ServiceBus:orders:QueueName"] = "orders",
            ["ServiceBus:orders:RequiredSession"] = "true",
            ["FunctionApp:notify:BaseUrl"] = "https://example.test",
            ["FunctionApp:notify:Code"] = "secret",
        });

        DefaultConfigProvider provider = new();
        ServiceBusConfig busConfig = provider.LoadServiceBusConfig(configuration, "orders");
        FunctionAppConfig functionAppConfig = provider.LoadFunctionAppConfig(configuration, "notify");
        CosmosContainerDbConfig cosmosConfig = provider.LoadCosmosDbConfig(BuildConfiguration(new Dictionary<string, string?>
        {
            ["CosmosDb:cosmos:ConnectionString"] = "AccountEndpoint=https://cosmos.test/",
            ["CosmosDb:cosmos:DatabaseName"] = "db",
            ["CosmosDb:cosmos:ContainerName"] = "items",
        }), "cosmos");

        Assert.Equal(new[] { "orders" }, provider.LoadAllServiceBusIdentifier(configuration));
        Assert.Equal(new[] { "notify" }, provider.LoadAllFunctionAppIdentifier(configuration));
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
        Assert.Equal("https://functions.test", provider.GetRequiredService<ConfigStore<FunctionAppConfig>>().GetConfig("func").BaseUrl);
        Assert.Equal("db", provider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig("cosmos").DatabaseName);
        Assert.Equal("queue", provider.GetRequiredService<ConfigStore<ServiceBusConfig>>().GetConfig("bus").EntityName);
        Assert.Equal("blob", provider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig("storage").BlobContainerNameRequired);
        Assert.Equal("main", provider.GetRequiredService<ConfigStore<SqlDatabaseConfig>>().GetConfig("sql").DatabaseName);
    }

    [Fact]
    public async Task ServiceBusSendTrigger_UsesUnifiedFactoryAndAssignsSessionId()
    {
        FakeServiceBusSender sender = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(CreateStore("bus", new ServiceBusConfig
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
            services.AddSingleton(CreateStore("func", new FunctionAppConfig
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

        HttpResponseMessage? actual = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Same(response, actual);
        Assert.Equal("https://example.test/api/orders", sender.Request!.RequestUri!.ToString());
        Assert.Equal("secret", sender.Request.Headers.GetValues("x-functions-key").Single());
        Assert.Equal("1", sender.Request.Headers.GetValues("x-test").Single());
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
            services.AddSingleton(CreateStore("func", new FunctionAppConfig
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
            services.AddSingleton(CreateStore("func", new FunctionAppConfig
            {
                BaseUrl = "https://example.test/api/",
                Code = "function-code",
                AdminCode = "admin-code",
            }));
            services.AddSingleton(new FunctionAppTriggerConfig { DoPing = false });
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { HttpFactory = new FakeHttpComponentFactory(sender) });
        });

        Step<object?> trigger = AzureTF.Trigger.IsLive.FunctionApp("func");

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Equal("https://example.test/api/admin/host/status", sender.Request!.RequestUri!.ToString());
        Assert.Equal("admin-code", sender.Request.Headers.GetValues("x-functions-key").Single());
    }

    [Fact]
    public async Task ServiceBusIsLiveTrigger_UsesAdministrationValidation()
    {
        FakeServiceBusAdministrationAdapter administration = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(CreateStore("bus", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = "orders",
                TopicName = null,
                SubscriptionName = null,
                RequiredSession = false,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { ServiceBusFactory = new FakeServiceBusComponentFactory(admin: administration) });
        });

        Step<object?> trigger = AzureTF.Trigger.IsLive.ServiceBus("bus");

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(administration.ValidateCalled);
    }

    [Fact]
    public async Task BlobStorageIsLiveTrigger_UsesContainerValidation()
    {
        FakeBlobContainerAdapter container = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(CreateStore("storage", new StorageAccountConfig
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "blob",
                QueueContainerName = null,
                TableContainerName = "table",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { BlobFactory = new FakeBlobComponentFactory(container) });
        });

        Step<object?> trigger = AzureTF.Trigger.IsLive.Blob("storage");

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(container.ValidateCalled);
    }

    [Fact]
    public async Task TableStorageIsLiveTrigger_UsesTableValidation()
    {
        FakeTableAdapter table = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(CreateStore("storage", new StorageAccountConfig
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "blob",
                QueueContainerName = null,
                TableContainerName = "table",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { TableFactory = new FakeTableComponentFactory(table) });
        });

        Step<object?> trigger = AzureTF.Trigger.IsLive.Table("storage");

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(table.ValidateCalled);
        Assert.Equal("table", table.LastTableName);
    }

    [Fact]
    public async Task CosmosContainerIsLiveTrigger_UsesContainerValidation()
    {
        FakeCosmosContainerAdapter container = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(CreateStore("cosmos", new CosmosContainerDbConfig
            {
                ConnectionString = "AccountEndpoint=https://cosmos.test/",
                DatabaseName = "db",
                ContainerName = "items",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { CosmosFactory = new FakeCosmosComponentFactory(container) });
        });

        Step<object?> trigger = AzureTF.Trigger.IsLive.Cosmos("cosmos");

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(container.ValidateCalled);
    }

    [Fact]
    public async Task CosmosContainerIsLiveTrigger_UsesConfiguredTimeoutForLocalEndpoint()
    {
        FakeCosmosContainerAdapter container = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(CreateStore("cosmos", new CosmosContainerDbConfig
            {
                ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=emulator;",
                DatabaseName = "db",
                ContainerName = "items",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { CosmosFactory = new FakeCosmosComponentFactory(container) });
        });

        Step<object?> trigger = AzureTF.Trigger.IsLive.Cosmos("cosmos");
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
            services.AddSingleton(CreateStore("cosmos", new CosmosContainerDbConfig
            {
                ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=emulator;",
                DatabaseName = "db",
                ContainerName = "items",
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { CosmosFactory = new FakeCosmosComponentFactory(container) });
        });

        Step<object?> trigger = AzureTF.Trigger.IsLive.Cosmos("cosmos");
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
            services.AddSingleton(CreateStore("cosmos", new CosmosContainerDbConfig
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
            services.AddSingleton(CreateStore("cosmos", new CosmosContainerDbConfig
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
            services.AddSingleton(CreateStore("cosmos", new CosmosContainerDbConfig
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
    public async Task StorageAccountBlobArtifactDescriber_UsesUnifiedFactoryContainer()
    {
        FakeBlobContainerAdapter container = new();
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(CreateStore("storage", new StorageAccountConfig
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
            services.AddSingleton(CreateStore("storage", new StorageAccountConfig
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
            services.AddSingleton(CreateStore("storage", new StorageAccountConfig
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
            services.AddSingleton(CreateStore("storage", new StorageAccountConfig
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
            services.AddSingleton(CreateStore("storage", new StorageAccountConfig
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
            services.AddSingleton(CreateStore("topic", new ServiceBusConfig
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
            services.AddSingleton(CreateStore("topic", new ServiceBusConfig
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
            services.AddSingleton(CreateStore("topic", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = null,
                TopicName = "orders-topic",
                SubscriptionName = "default-sub",
                RequiredSession = false,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { ServiceBusFactory = new FakeServiceBusComponentFactory(pump: pump) });
        });

        ServiceBusProcessEvent step = AzureTF.Event.ServiceBus.MessageReceived(
            "topic",
            messageId: Var.Const("m1"),
            correlationId: Var.Const("c1"),
            predicate: Var.Const<Func<ServiceBusReceivedMessage, bool>>(message => message.MessageId == "m1"),
            completeMessage: Var.Const(true));

        ServiceBusReceivedMessage? result = await step.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Same(receivedMessage, result);
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
            services.AddSingleton(CreateStore("queue", new ServiceBusConfig
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
        ServiceBusSendTrigger sendTrigger = AzureTF.Trigger.ServiceBus.Send("queue", Var.Const(sendMessage));
        ServiceBusProcessEvent receiveEvent = AzureTF.Event.ServiceBus.MessageReceived("queue", messageId: Var.Const("q1"));

        await sendTrigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);
        ServiceBusReceivedMessage? result = await receiveEvent.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Same(sendMessage, sender.MessageSent);
        Assert.Same(receivedMessage, result);
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
            services.AddSingleton(CreateStore("queue", new ServiceBusConfig
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
        ServiceBusSendTrigger sendTrigger = AzureTF.Trigger.ServiceBus.Send("queue", Var.Const(sendMessage));
        ServiceBusProcessEvent receiveEvent = AzureTF.Event.ServiceBus.MessageReceived("queue", messageId: Var.Const("q2"));

        await sendTrigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);
        ServiceBusReceivedMessage? result = await receiveEvent.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Same(sendMessage, sender.MessageSent);
        Assert.False(string.IsNullOrWhiteSpace(sendMessage.SessionId));
        Assert.Same(receivedMessage, result);
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
            services.AddSingleton(CreateStore("topic", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = null,
                TopicName = "orders-topic",
                SubscriptionName = null,
                RequiredSession = false,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory { ServiceBusFactory = new FakeServiceBusComponentFactory(pump: pump) });
        });

        ServiceBusProcessEvent receiveEvent = AzureTF.Event.ServiceBus.MessageReceived("topic", messageId: Var.Const("t1"), createTempSubscription: true);

        StepGeneric? preStep = ((IHasPreStep)receiveEvent).CreatePreStep(runtime.VariableStore);
        StepGeneric? cleanupStep = ((IHasCleanupStep)receiveEvent).CreateCleanupStep(runtime.VariableStore);
        ServiceBusReceivedMessage? result = await receiveEvent.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.IsType<ServiceBusCreateTempSubscriptionStep>(preStep);
        Assert.IsType<ServiceBusDeleteTempSubscriptionStep>(cleanupStep);
        Assert.Same(receivedMessage, result);
        Assert.NotNull(pump.SubscriptionName);
        Assert.StartsWith("tmp-", pump.SubscriptionName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServiceBusProcessEvent_ThrowsWhenTopicConfigIsMissingSubscription()
    {
        RuntimeContext runtime = RuntimeContext.Create(services =>
        {
            services.AddSingleton(CreateStore("topic", new ServiceBusConfig
            {
                ConnectionString = "Endpoint=sb://orders/",
                QueueName = null,
                TopicName = "orders-topic",
                SubscriptionName = null,
                RequiredSession = false,
            }));
            services.AddSingleton<IAzureComponentFactory>(new FakeAzureComponentFactory());
        });

        ServiceBusProcessEvent receiveEvent = AzureTF.Event.ServiceBus.MessageReceived("topic", messageId: Var.Const("missing-sub"));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            receiveEvent.DoEventPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None));

        Assert.Contains("createTempSubscription", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static ConfigStore<TConfig> CreateStore<TConfig>(string key, TConfig config)
    {
        ConfigStore<TConfig> store = new();
        store.AddConfig(key, config);
        return store;
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

            VariableStore = null!;
            ArtifactStore = null!;
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
        public bool ValidateCalled { get; private set; }
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

        public Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            ValidateCalled = true;
            return ValidateConnectionAsyncHandler?.Invoke(cancellationToken) ?? Task.CompletedTask;
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
        public bool ValidateCalled { get; private set; }
        public string? UploadedPath { get; private set; }
        public byte[]? UploadedData { get; private set; }
        public IReadOnlyDictionary<string, string>? UploadedMetadata { get; private set; }
        public string? DeletedPath { get; private set; }
        public string? ReadPath { get; private set; }
        public BlobReadResponse ReadResponse { get; set; } = new(false, null, null);

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
        public bool ValidateCalled { get; private set; }
        public string? LastTableName { get; set; }
        public object? UpsertedEntity { get; private set; }
        public (string PartitionKey, string RowKey)? DeletedEntity { get; private set; }
        public (string PartitionKey, string RowKey)? ReadKeys { get; private set; }
        public object? ReadEntity { get; set; }
        public string? LastFilter { get; private set; }
        public List<object> QueryResults { get; } = [];

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
        public bool ValidateCalled { get; private set; }
        public CreateSubscriptionOptions? CreatedOptions { get; private set; }
        public CreateRuleOptions? CreatedRuleOptions { get; private set; }
        public (string TopicName, string SubscriptionName)? DeletedSubscription { get; private set; }

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

    private sealed class FakeHttpRequestSender(HttpResponseMessage response) : IHttpRequestSender
    {
        public HttpRequestMessage? Request { get; private set; }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(response);
        }
    }

    private sealed record TestItem(string id, string partitionKey, string name);

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
