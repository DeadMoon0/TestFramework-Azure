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

## Sample: Service Bus Send + Wait

```csharp
using Azure.Messaging.ServiceBus;
using TestFrameworkAzure;
using TestFramework.Core.Timelines;

Timeline timeline = Timeline.Create()
    .Trigger(AzureTF.Trigger.ServiceBus.Send("MainSBTopic",
        new ServiceBusMessage("Test message") { CorrelationId = "1234 :)" }))
    .WaitForEvent(AzureTF.Event.ServiceBus.MessageReceived(
        "MainSBTopic",
        correlationId: "1234 :)",
        completeMessage: true))
    .WithTimeOut(TimeSpan.FromSeconds(10))
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
