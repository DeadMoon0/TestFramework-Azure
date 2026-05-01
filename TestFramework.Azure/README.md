# TestFramework.Azure

## Introduction

TestFramework.Azure is an extension package for TestFramework.Core.

If you are new: TestFrameworkCore runs your timeline, and this package adds Azure-specific triggers, events, and artifact helpers.
You usually install and learn TestFrameworkCore first, then add TestFrameworkAzure when your tests need Azure resources.

Azure-focused test helpers for TestFramework.Core timelines.

TestFramework.Azure adds fluent building blocks for:
- Azure Function App calls (remote and in-process)
- Service Bus send/receive flows
- Azure artifacts (SQL, Cosmos DB, Table Storage, Blob Storage)

## Install

```bash
dotnet add package TestFramework.Azure
```

## Minimal Setup

```csharp
using TestFramework.Azure.Extensions;
using TestFramework.Config;

ConfigInstance config = ConfigInstance.FromJsonFile("local.testSettings.json")
    .LoadAzureConfig()
    .Build();
```

## Configuration Records

The Azure package reads named records from configuration sections through `LoadAzureConfig()` or `LoadAzureConfigs(...)`.
Each DSL identifier such as `"MainDb"` or `"Default"` maps to one child object inside the matching section.

Supported record types:

| Section | Record type | Required fields | Optional fields | Used by |
|---------|-------------|-----------------|-----------------|---------|
| `FunctionApp:{identifier}` | `FunctionAppConfig` | `BaseUrl`, `Code` | `AdminCode` | Remote Function App HTTP triggers and Function App liveness checks |
| `CosmosDb:{identifier}` | `CosmosContainerDbConfig` | `ConnectionString`, `DatabaseName`, `ContainerName` | None | Cosmos artifacts, query finders, and Cosmos liveness checks |
| `ServiceBus:{identifier}` | `ServiceBusConfig` | `ConnectionString` plus either `QueueName` or `TopicName` | `SubscriptionName`, `RequiredSession` | Service Bus send triggers and message-received events |
| `StorageAccount:{identifier}` | `StorageAccountConfig` | `ConnectionString` | `QueueContainerName`, `BlobContainerName`, `TableContainerName` | Blob artifacts, table artifacts, and storage liveness checks |
| `SqlDatabase:{identifier}` | `SqlDatabaseConfig` | `ConnectionString`, `DatabaseName` | `ContextType` | SQL row artifacts, SQL query finders, and SQL liveness checks |

Configuration expectations:

- `FunctionAppConfig.BaseUrl` should be the host root, usually ending at the site root rather than a specific function route.
- `FunctionAppConfig.Code` is the normal trigger key; `AdminCode` is only needed when host-status checks require a different admin-level key.
- `CosmosContainerDbConfig` is container-specific. One identifier points to one database/container pair.
- `ServiceBusConfig` should define either queue mode or topic mode. Queue mode uses `QueueName`. Topic mode uses `TopicName`, and fixed-subscription receives also require `SubscriptionName`.
- `StorageAccountConfig` only requires container names for the features you use. Blob and table liveness checks rely on `BlobContainerName` and `TableContainerName` respectively.
- `SqlDatabaseConfig.ContextType` is an assembly-qualified `DbContext` type name. Leave it unset when you register the context through the SQL registry instead.

Example JSON:

```json
{
    "FunctionApp": {
        "Default": {
            "BaseUrl": "https://my-functions.azurewebsites.net/",
            "Code": "function-key",
            "AdminCode": "admin-key"
        }
    },
    "CosmosDb": {
        "MainDb": {
            "ConnectionString": "AccountEndpoint=...;AccountKey=...;",
            "DatabaseName": "AppDb",
            "ContainerName": "Orders"
        }
    },
    "ServiceBus": {
        "MainSBQueue": {
            "ConnectionString": "Endpoint=sb://...",
            "QueueName": "orders",
            "RequiredSession": false
        },
        "MainSBTopic": {
            "ConnectionString": "Endpoint=sb://...",
            "TopicName": "events",
            "SubscriptionName": "integration-tests",
            "RequiredSession": false
        }
    },
    "StorageAccount": {
        "MainStorage": {
            "ConnectionString": "DefaultEndpointsProtocol=https;...",
            "BlobContainerName": "exports",
            "TableContainerName": "OrderAudit"
        }
    },
    "SqlDatabase": {
        "MainSql": {
            "ConnectionString": "Server=...;Database=AppDb;...",
            "DatabaseName": "AppDb",
            "ContextType": "MyProject.Data.AppDbContext, MyProject"
        }
    }
}
```

## Sample: Function App HTTP Call

```csharp
using TestFramework.Azure;
using TestFramework.Core.Timelines;

Timeline timeline = Timeline.Create()
    .Trigger(AzureTF.Trigger.FunctionApp.Http("Default").SelectEndpointWithMethod<HttpTests>(nameof(HttpTests.Run)).Call())
    .Build();

TimelineRun run = await timeline.SetupRun(config.BuildServiceProvider()).RunAsync();

run.EnsureRanToCompletion();
```

`SelectEndpointWithMethod<T>(...)` expects the target method to carry both a `[Function(...)]` attribute and a parameter marked with `[HttpTrigger(...)]`.

## Function App Execution Modes

`AzureTF.Trigger.FunctionApp` exposes three different execution styles:

| Mode | Use when | Runtime dependency | Typical benefit |
|------|----------|--------------------|-----------------|
| `Http(...)` | you want to call a deployed or container-hosted Function App over HTTP | reachable HTTP host plus `FunctionAppConfig` | closest to production wiring |
| `Managed<T>(identifier, method)` | you want framework-managed invocation of a known function entry point | the function type is available to the test process | avoids hand-written HTTP request setup |
| `InProcessHttp<T>(...)` | you want to execute the function handler directly in-process | the function type and request delegate are available in the test process | fastest feedback and easiest offline unit-style validation |

Choose `Http(...)` for end-to-end behavior, `Managed<T>(...)` when you still want a Function App abstraction without a remote hop, and `InProcessHttp<T>(...)` when the test should stay entirely local to the current process.

## `InProcessHttp(...)` Overload Guide

Use the smallest overload that matches what your function returns:

- `InProcessHttp<T>((request, context) => { ... })` for synchronous handlers that return no result.
- `InProcessHttp<T>((request, context) => Task.CompletedTask)` for asynchronous handlers that return no result.
- `InProcessHttp<T>((request, context) => new OkResult())` for synchronous handlers that return `IActionResult`.
- `InProcessHttp<T>((request, context) => Task.FromResult<IActionResult>(...))` for asynchronous handlers that return `IActionResult`.

For remote calls, the equivalent decision point is different: start with `Http(...)`, then choose either `SelectEndpointWithMethod<T>(...)` when the route can be inferred from the function metadata, or `SelectEndpoint(path, method)` when the test should supply the path and HTTP verb explicitly.

Example with explicit path, body, and headers:

```csharp
Timeline timeline = Timeline.Create()
    .Trigger(
        AzureTF.Trigger.FunctionApp.Http("Default")
            .SelectEndpoint(Var.Const("orders/42"), Var.Const(HttpMethod.Post))
            .WithHeader(Var.Const("x-correlation-id"), Var.Const("order-42"))
            .WithHeaders(Var.Const(new Dictionary<string, string> { ["x-tenant"] = "lab" }))
            .WithBody(Var.Const("{\"id\":42}"))
            .Call())
    .Build();
```

## Service Bus Support Matrix

`ServiceBusConfig` supports three receive modes:

| Mode | Required fields | Notes |
|------|-----------------|-------|
| Queue | `ConnectionString`, `QueueName` | `SubscriptionName` must be omitted. Works with session and non-session queues. |
| Topic + subscription | `ConnectionString`, `TopicName`, `SubscriptionName` | Use when the test should receive from a fixed subscription. |
| Topic + temp subscription | `ConnectionString`, `TopicName` | Call `AzureTF.Event.ServiceBus.MessageReceived(..., createTempSubscription: true)` to create and clean up a filtered temp subscription automatically. |

## Sample: Service Bus Queue Send + Wait

```csharp
using Azure.Messaging.ServiceBus;
using TestFramework.Azure;
using TestFramework.Core.Timelines;

Timeline timeline = Timeline.Create()
    .Trigger(AzureTF.Trigger.ServiceBus.Send("MainSBQueue", new ServiceBusMessage("Test message") { CorrelationId = "order-42" }))
    .WaitForEvent(AzureTF.Event.ServiceBus.MessageReceived("MainSBQueue", correlationId: "order-42", completeMessage: true))
        .WithTimeOut(TimeSpan.FromSeconds(10))
    .Build();
```

## Sample: Service Bus Topic Send + Temp Subscription Wait

```csharp
Timeline timeline = Timeline.Create()
    .WaitForEvent(AzureTF.Event.ServiceBus.MessageReceived("MainSBTopic", correlationId: "topic-1234", createTempSubscription: true, completeMessage: true))
        .WithTimeOut(TimeSpan.FromSeconds(10))
    .Trigger(AzureTF.Trigger.ServiceBus.Send("MainSBTopic", new ServiceBusMessage("Test message") { CorrelationId = "topic-1234" }))
    .Build();
```

## Cosmos Client Options Note

Use `ConfigureCosmosClientOptions(...)` when you need to customize the underlying Cosmos SDK client.

For `AzureTF.Trigger.IsLive.Cosmos(...)`, timeout control comes from the normal timeline step timeout, for example via `.WithTimeOut(...)` on the builder. The optional `AlivenessLevel` lets you choose whether the check should stop at endpoint reachability, account authentication, or require the configured container to exist.

```csharp
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure;
using TestFramework.Azure.Extensions;
using TestFramework.Config;
using TestFramework.Core.Timelines;

ConfigInstance config = ConfigInstance.FromJsonFile("local.testSettings.json")
    .AddService((services, configuration) =>
    {
        services.LoadAzureConfigs(configuration)
            .ConfigureCosmosClientOptions(_ => new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
            });
    })
    .Build();

Timeline timeline = Timeline.Create()
    .Trigger(AzureTF.Trigger.IsLive.Cosmos("MainDb", AlivenessLevel.Authenticated))
        .WithTimeOut(TimeSpan.FromSeconds(5))
    .Build();
```

## Sample: Find Data Artifact

```csharp
using Microsoft.Azure.Cosmos;
using TestFramework.Azure;

Timeline timeline = Timeline.Create()
    .FindArtifactMulti(
        ["cosmosItemQuery"],
        AzureTF.ArtifactFinder.DB.CosmosQuery<MyCosmosItem>(
            "MainDb",
            new QueryDefinition("SELECT * FROM c WHERE c.number = 1")))
    .Build();
```

## Target Framework

- .NET 8 (`net8.0`)
