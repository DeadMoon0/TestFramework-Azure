# TestFrameworkAzure

## Introduction

TestFrameworkAzure is an extension package for TestFrameworkCore.

If you are new: TestFrameworkCore runs your timeline, and this package adds Azure-specific triggers, events, and artifact helpers.
You usually install and learn TestFrameworkCore first, then add TestFrameworkAzure when your tests need Azure resources.

Azure-focused test helpers for TestFrameworkCore timelines.

TestFrameworkAzure adds fluent building blocks for:
- Azure Function App calls (remote and in-process)
- Service Bus send/receive flows
- Azure artifacts (SQL, Cosmos DB, Table Storage, Blob Storage)

## Install

```bash
dotnet add package TestFrameworkAzure
```

## Minimal Setup

```csharp
using TestFrameworkAzure.Extensions;
using TestFramework.Config;

ConfigInstance config = ConfigInstance.FromJsonFile("local.testSettings.json")
    .SetupSubInstance()
    .LoadAzureConfig()
    .Build();
```

## Sample: Function App HTTP Call

```csharp
using FunctionApp;
using TestFrameworkAzure;
using TestFramework.Core.Timelines;

Timeline timeline = Timeline.Create()
    .Trigger(AzureTF.Trigger.FunctionApp
        .Http("Default")
        .SelectEndpointWithMethod<HttpTests>(nameof(HttpTests.Run))
        .Call())
    .Build();

TimelineRun run = await timeline.SetupRun(config.BuildServiceProvider())
    .RunAsync();

run.EnsureRanToCompletion();
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
using TestFrameworkAzure;
using TestFramework.Core.Timelines;

Timeline timeline = Timeline.Create()
    .Trigger(AzureTF.Trigger.ServiceBus.Send("MainSBQueue",
        new ServiceBusMessage("Test message") { CorrelationId = "1234 :)" }))
    .WaitForEvent(AzureTF.Event.ServiceBus.MessageReceived(
        "MainSBQueue",
        correlationId: "1234 :)",
        completeMessage: true))
    .WithTimeOut(TimeSpan.FromSeconds(10))
    .Build();
```

## Sample: Service Bus Topic Send + Temp Subscription Wait

```csharp
Timeline timeline = Timeline.Create()
    .WaitForEvent(AzureTF.Event.ServiceBus.MessageReceived(
        "MainSBTopic",
        correlationId: "topic-1234",
        createTempSubscription: true,
        completeMessage: true))
    .Trigger(AzureTF.Trigger.ServiceBus.Send("MainSBTopic",
        new ServiceBusMessage("Test message") { CorrelationId = "topic-1234" }))
    .WithTimeOut(TimeSpan.FromSeconds(10))
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
using TestFrameworkAzure;

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
