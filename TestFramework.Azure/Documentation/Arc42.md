# TestFrameworkAzure — arc42 Architecture Documentation

> **Version:** 1.1
> **Date:** April 2026
> **Audience:** Developers writing Azure integration tests with the TestFramework

---

## Table of Contents

1. [Introduction and Goals](#1-introduction-and-goals)
2. [Constraints](#2-constraints)
3. [System Scope and Context](#3-system-scope-and-context)
4. [Solution Strategy](#4-solution-strategy)
5. [Building Block View](#5-building-block-view)
6. [Runtime View](#6-runtime-view)
7. [Deployment View](#7-deployment-view)
8. [Cross-Cutting Concepts](#8-cross-cutting-concepts)
9. [Architecture Decisions](#9-architecture-decisions)
10. [Quality Requirements](#10-quality-requirements)
11. [Risks and Technical Debt](#11-risks-and-technical-debt)
12. [Glossary](#12-glossary)
13. [Quickstart](#13-quickstart)

---

## 1. Introduction and Goals

### 1.1 Purpose

**TestFrameworkAzure** is the Azure extension package for TestFrameworkCore. It provides ready-to-use implementations for:

- **Triggers** — Azure Functions (HTTP, Managed, InProcess), sending Service Bus messages
- **Artifacts** — Blob Storage, Table Storage, Cosmos DB, SQL Server (EF Core) — automatic setup, versioning, and teardown
- **Events** — Receiving Service Bus messages (with temporary subscriptions)
- **Configuration** — Central management of connection strings and service settings via typed identifiers

**Core goal:** A team member can write a complete Azure integration test — from configuration to assertion — using only this documentation.

### 1.2 Quality Goals

| Priority | Goal | Description |
|----------|------|-------------|
| 1 | **Full Azure coverage** | Blob, Table, Cosmos, SQL, ServiceBus, FunctionApp as first-class citizens |
| 2 | **Fluent API** | `AzureTF.Trigger.FunctionApp.Http(…)` — readable, type-safe, IDE-friendly |
| 3 | **Automatic lifecycle** | Artifacts are automatically torn down after the test |
| 4 | **Identifier-based config** | Multiple instances of the same service type can be configured in parallel |
| 5 | **Isolation** | Service Bus tests use temporary subscriptions with server-side filtering |

### 1.3 Stakeholders

| Role | Expectation |
|------|-------------|
| Test developer | Simple API for Azure step definition and assertion |
| DevOps / Infra | Clear configuration structure (JSON sections, connection strings) |
| Framework developer | Extensible patterns for new Azure services |

---

## 2. Constraints

### 2.1 Technical

| Constraint | Detail |
|------------|--------|
| Runtime | .NET 8.0 |
| Azure Functions | Isolated Worker Model (dotnet-isolated) |
| ORM | Entity Framework Core 8.0 (SQL Server provider) |
| Configuration | Microsoft.Extensions.Configuration + DependencyInjection v10.0 |
| Azure SDKs | Azure.Storage.Blobs 12.26, Azure.Data.Tables 12.9, Azure.Messaging.ServiceBus 7.20, Microsoft.Azure.Cosmos 3.57 |

### 2.2 Organisational

| Constraint | Detail |
|------------|--------|
| Dependency | Builds on `TestFrameworkCore` (engine) and `TestFrameworkConfig` (configuration) |
| Config source | JSON file (e.g. `local.testSettings.json`) with sections per Azure service |
| Azure resources | Must be pre-provisioned — the framework does not create infrastructure |

---

## 3. System Scope and Context

### 3.1 Business Context

```mermaid
C4Context
    title TestFrameworkAzure — Business Context

    Person(dev, "Test Developer", "Writes Azure integration tests")
    System(azure_tf, "TestFrameworkAzure", "Azure extension for the test framework")
    System_Ext(core, "TestFrameworkCore", "Timeline, Steps, Variables, Artifacts")
    System_Ext(config, "TestFrameworkConfig", "ConfigInstance, DI builder")
    System_Ext(xunit, "xUnit", "Test runner")
    System_Ext(blob, "Azure Blob Storage", "Files / blobs")
    System_Ext(table, "Azure Table Storage", "Key-value entities")
    System_Ext(cosmos, "Azure Cosmos DB", "Documents")
    System_Ext(sql, "Azure SQL / SQL Server", "Relational data")
    System_Ext(sb, "Azure Service Bus", "Queues & topics")
    System_Ext(func, "Azure Functions", "Serverless compute")

    Rel(dev, azure_tf, "Defines tests with AzureTF.*")
    Rel(azure_tf, core, "Implements Steps, Artifacts, Events")
    Rel(azure_tf, config, "Loads configs via ConfigInstance")
    Rel(azure_tf, blob, "Upload / Download / Delete")
    Rel(azure_tf, table, "Upsert / Get / Delete entities")
    Rel(azure_tf, cosmos, "Upsert / Read / Delete items")
    Rel(azure_tf, sql, "EF Core CRUD + migrations")
    Rel(azure_tf, sb, "Send / Receive messages")
    Rel(azure_tf, func, "HTTP / Managed / InProcess trigger")
    Rel(azure_tf, xunit, "ITestOutputHelper for logging")
```

### 3.2 Technical Context — Dependencies

```mermaid
graph TB
    subgraph TestFrameworkAzure
        ATF[AzureTF static API]
        CFG[Configuration]
        TRG[Triggers]
        ART[Artifacts]
        EVT[Events]
    end
    subgraph NuGet packages
        ASB[Azure.Messaging.ServiceBus]
        ADT[Azure.Data.Tables]
        ABLOB[Azure.Storage.Blobs]
        MAC[Microsoft.Azure.Cosmos]
        EFC[Microsoft.EntityFrameworkCore]
        EFCS[EFCore.SqlServer]
        AFW[Azure.Functions.Worker]
    end
    subgraph Project references
        CORE[TestFrameworkCore]
        CONF[TestFrameworkConfig]
    end
    ATF --> TRG
    ATF --> ART
    ATF --> EVT
    TRG --> ASB
    TRG --> AFW
    EVT --> ASB
    ART --> ADT
    ART --> ABLOB
    ART --> MAC
    ART --> EFC
    ART --> EFCS
    ATF --> CORE
    CFG --> CONF
```

---

## 4. Solution Strategy

| Decision | Rationale |
|----------|-----------|
| **Static entry-point `AzureTF`** | Single discoverable API surface — `AzureTF.Trigger.*`, `AzureTF.Artifact.*`, etc. |
| **Identifier pattern** | Each Azure service instance is addressed by an identifier → multiple instances in parallel |
| **Artifact triple (Reference/Data/Describer)** | Reuses the Core CRTP pattern per Azure service |
| **Staged builder for Function App HTTP** | Interface-based stages prevent incomplete configurations |
| **Service Bus temp subscriptions** | Test isolation via per-run subscriptions with server-side correlation filters |
| **SQL: DbContext registry + 3-tier resolution** | Flexible Identifier → DbContext mapping without requiring compile-time binding |

---

## 5. Building Block View

### 5.1 Level 1 — Overview

```mermaid
graph TB
    subgraph TestFrameworkAzure
        ATF[AzureTF]
        subgraph Triggers
            FHTTP[HttpRemoteFunctionAppTrigger]
            FMAN[ManagedRemoteFunctionAppTrigger]
            FIP["InProcessHttpFunctionAppTrigger&lt;T&gt;"]
            SBS[ServiceBusSendTrigger]
        end
        subgraph Artifacts
            BLOB[StorageAccountBlobArtifact*]
            TBL["TableStorageEntityArtifact*&lt;T&gt;"]
            COSM["CosmosDbItemArtifact*&lt;T&gt;"]
            SQLR["SqlRowArtifact*&lt;T&gt;"]
        end
        subgraph Events
            SBE[ServiceBusProcessEvent]
        end
        subgraph Finders
            COSMF["CosmosDbItemArtifactQueryFinder&lt;T&gt;"]
            SQLF["SqlEFCoreArtifactQueryFinder&lt;T&gt;"]
            TBLF["TableStorageEntityArtifactQueryFinder&lt;T&gt;"]
        end
        subgraph Configuration
            CL[ConfigLoader]
            CS["ConfigStore&lt;T&gt;"]
        end
    end
    ATF --> FHTTP
    ATF --> FMAN
    ATF --> FIP
    ATF --> SBS
    ATF --> SBE
    ATF --> BLOB
    ATF --> TBL
    ATF --> COSM
    ATF --> SQLR
    ATF --> COSMF
    ATF --> SQLF
    ATF --> TBLF
```

### 5.2 AzureTF — Static API Structure

```mermaid
classDiagram
    class AzureTF {
        <<static>>
        +TriggerProxy Trigger
        +ArtifactProxy Artifact
        +ArtifactFinderProxy ArtifactFinder
        +EventProxy Event
    }
    class FunctionAppTriggerProxy {
        +Http(id) IFunctionAppHttpConnectionStage
        +Managed~T~(id, method) ManagedRemoteFunctionAppTrigger
        +InProcessHttp~T~(action) IFunctionAppHttpPayloadStage
    }
    class ServiceBusTriggerProxy {
        +Send(id, message) ServiceBusSendTrigger
    }
    class DbArtifactProxy {
        +CosmosRef~T~(id, docId, pk) CosmosDbItemArtifactReference~T~
        +SqlRef~T~(id, keys) SqlRowArtifactReference~T~
    }
    class StorageAccountArtifactProxy {
        +BlobRef(id, path) StorageAccountBlobArtifactReference
        +TableRef~T~(id, table, pk, rk) TableStorageEntityArtifactReference~T~
    }
    class ServiceBusEventProxy {
        +MessageReceived(id, ...) ServiceBusProcessEvent
    }
    AzureTF --> FunctionAppTriggerProxy
    AzureTF --> ServiceBusTriggerProxy
    AzureTF --> DbArtifactProxy
    AzureTF --> StorageAccountArtifactProxy
    AzureTF --> ServiceBusEventProxy
```

### 5.3 Artifact Implementations per Azure Service

```mermaid
classDiagram
    class ArtifactReferenceGeneric {<<abstract / Core>>}
    class ArtifactDataGeneric {<<abstract / Core>>}
    class ArtifactDescriberGeneric {<<abstract / Core>>}

    class StorageAccountBlobArtifactReference {
        +StorageAccountIdentifier Identifier
        +VariableReference~string~ Path
        +ResolveToDataAsync() download blob
    }
    class StorageAccountBlobArtifactData {
        +byte[] Data
        +IDictionary Metadata
    }
    class StorageAccountBlobArtifactDescriber {
        +Setup() upload blob
        +Deconstruct() delete blob
    }

    class TableStorageEntityArtifactReference~T~ {
        +StorageAccountIdentifier Identifier
        +VariableReference~string~ TableName, PartitionKey, RowKey
        +ResolveToDataAsync() GetEntity
    }
    class TableStorageEntityArtifactData~T~ { +T Entity }
    class TableStorageEntityArtifactDescriber~T~ {
        +Setup() UpsertEntity
        +Deconstruct() DeleteEntity
    }

    class CosmosDbItemArtifactReference~T~ {
        +CosmosContainerIdentifier Identifier
        +string Id, PartitionKey
        +ResolveToDataAsync() ReadItemStream
    }
    class CosmosDbItemArtifactData~T~ { +T Item }
    class CosmosDbItemArtifactDescriber~T~ {
        +Setup() UpsertItemAsync
        +Deconstruct() DeleteItemAsync
    }

    class SqlRowArtifactReference~T~ {
        +SqlDatabaseIdentifier Identifier
        +string[] PrimaryKeyValues
        +ResolveToDataAsync() FindAsync via EF Core
    }
    class SqlRowArtifactData~T~ { +T Row }
    class SqlRowArtifactDescriber~T~ {
        +Setup() Add/Update + SaveChangesAsync
        +Deconstruct() Remove + SaveChangesAsync
    }

    ArtifactReferenceGeneric <|-- StorageAccountBlobArtifactReference
    ArtifactReferenceGeneric <|-- TableStorageEntityArtifactReference~T~
    ArtifactReferenceGeneric <|-- CosmosDbItemArtifactReference~T~
    ArtifactReferenceGeneric <|-- SqlRowArtifactReference~T~
    ArtifactDataGeneric <|-- StorageAccountBlobArtifactData
    ArtifactDataGeneric <|-- TableStorageEntityArtifactData~T~
    ArtifactDataGeneric <|-- CosmosDbItemArtifactData~T~
    ArtifactDataGeneric <|-- SqlRowArtifactData~T~
    ArtifactDescriberGeneric <|-- StorageAccountBlobArtifactDescriber
    ArtifactDescriberGeneric <|-- TableStorageEntityArtifactDescriber~T~
    ArtifactDescriberGeneric <|-- CosmosDbItemArtifactDescriber~T~
    ArtifactDescriberGeneric <|-- SqlRowArtifactDescriber~T~
```

### 5.4 Function App Trigger — Three Modes

```mermaid
classDiagram
    class Step~TResult~ {<<abstract / Core>>}
    class HttpRemoteFunctionAppTrigger {
        +Step~HttpResponseMessage~
        +Calls deployed function via HTTP (Function Key auth)
    }
    class ManagedRemoteFunctionAppTrigger {
        +Step~ManagedResult~
        +Calls deployed function via admin endpoint (AdminCode auth)
    }
    class InProcessHttpFunctionAppTrigger~TFunction~ {
        +Step~HttpResponseMessage~
        +Invokes function class directly in test process
    }
    Step~TResult~ <|-- HttpRemoteFunctionAppTrigger
    Step~TResult~ <|-- ManagedRemoteFunctionAppTrigger
    Step~TResult~ <|-- InProcessHttpFunctionAppTrigger~TFunction~
```

### 5.5 Service Bus — Trigger + Event + Temp Subscription

```mermaid
classDiagram
    class ServiceBusSendTrigger {
        +Step~void~
        +Execute() send message to queue or topic
    }
    class ServiceBusProcessEvent {
        +AsyncEvent
        +FilterOptions: MessageId, CorrelationId, Predicate
        +Execute() wait for matching message
        +IHasPreStep → ServiceBusCreateTempSubscriptionStep
        +IHasCleanupStep → ServiceBusDeleteTempSubscriptionStep
    }
    class ServiceBusCreateTempSubscriptionStep {
        +Step~void~
        +Execute() CreateSubscription with CorrelationFilter rule
    }
    class ServiceBusDeleteTempSubscriptionStep {
        +Step~void~
        +Execute() DeleteSubscription
    }
    ServiceBusProcessEvent --> ServiceBusCreateTempSubscriptionStep : pre-step
    ServiceBusProcessEvent --> ServiceBusDeleteTempSubscriptionStep : cleanup step
```

### 5.6 Configuration System

```mermaid
classDiagram
    class IConfigProvider {
        <<interface>>
        +Load~T~(IConfiguration, identifier) T
    }
    class DefaultConfigProvider {
        +Load~FunctionAppConfig~(config, id) reads "FunctionApp:id"
        +Load~StorageAccountConfig~(config, id) reads "StorageAccount:id"
        +Load~CosmosContainerDbConfig~(config, id) reads "CosmosDb:id"
        +Load~ServiceBusConfig~(config, id) reads "ServiceBus:id"
        +Load~SqlDatabaseConfig~(config, id) reads "SqlDatabase:id"
    }
    class FunctionAppConfig { +string BaseUrl, Code, AdminCode }
    class StorageAccountConfig { +string ConnectionString, BlobContainerName, TableContainerName }
    class CosmosContainerDbConfig { +string ConnectionString, DatabaseName, ContainerName }
    class ServiceBusConfig { +string ConnectionString, QueueName, TopicName, SubscriptionName, RequiredSession }
    class SqlDatabaseConfig { +string ConnectionString, DatabaseName, ContextType }
    IConfigProvider <|.. DefaultConfigProvider
```

### 5.7 SQL Server — DbContext Resolution

```mermaid
graph TD
    REQ["SqlRowArtifactDescriber needs DbContext"] --> RES[SqlDbContextResolver]
    RES --> T1{"1. Identifier has registered context?"}
    T1 -->|Yes| CTX1[Registered context type]
    T1 -->|No| T2{"2. Config has ContextType string?"}
    T2 -->|Yes| CTX2["Activate via Assembly.GetType()"]
    T2 -->|No| T3{"3. Default context registered?"}
    T3 -->|Yes| CTX3[Default context]
    T3 -->|No| ERR[InvalidOperationException]
    CTX1 --> MIG[SqlMigrationTracker]
    CTX2 --> MIG
    CTX3 --> MIG
    MIG -->|First use for this DB| MIGRATE["MigrateAsync() or EnsureCreatedAsync()"]
    MIG -->|Already migrated| READY[Context ready for CRUD]
    MIGRATE --> READY
```

---

## 6. Runtime View

### 6.1 Scenario: Blob Upload → Function Trigger → Cosmos Assert

```mermaid
sequenceDiagram
    participant Test as Test method
    participant TL as Timeline
    participant BS as BlobDescriber
    participant BLOB as Azure Blob Storage
    participant FT as HttpRemoteFunctionAppTrigger
    participant FUNC as Azure Function App
    participant CD as CosmosDbDescriber
    participant COSMOS as Azure Cosmos DB
    participant TR as TimelineRun

    Test->>TL: .SetupArtifact("input-blob")
    Test->>TL: .Trigger(AzureTF.Trigger.FunctionApp.Http("myApp")...)
    Test->>TL: .RegisterArtifact("output-doc", cosmosRef)
    Test->>TL: .CaptureArtifactVersion("output-doc")
    Test->>TL: .Build() + .SetupRun() + .RunAsync()

    Note over TL: Main stage begins
    TL->>BS: Setup("input-blob")
    BS->>BLOB: UploadAsync(data, metadata)

    TL->>FT: Execute()
    FT->>FUNC: HTTP POST /api/process
    FUNC->>BLOB: Download blob
    FUNC->>COSMOS: Upsert result doc
    FUNC-->>FT: 200 OK

    TL->>CD: Resolve("output-doc")
    CD->>COSMOS: ReadItemStream(id, pk)

    Note over TL: Cleanup stage
    TL->>BS: Deconstruct("input-blob")
    BS->>BLOB: DeleteAsync()

    TL-->>TR: TimelineRun (frozen)
    Test->>TR: run.Artifact~CosmosData~("output-doc").Should()...
```

### 6.2 Scenario: Service Bus Send + Receive with Temp Subscription

```mermaid
sequenceDiagram
    participant TL as Timeline
    participant PRE as CreateTempSubscription
    participant SBT as ServiceBusSendTrigger
    participant SBE as ServiceBusProcessEvent
    participant CLN as DeleteTempSubscription
    participant SB as Azure Service Bus

    Note over TL: Pre-setup stage
    TL->>PRE: Execute()
    PRE->>SB: CreateSubscription("temp-xyz", CorrelationFilter)

    Note over TL: Main stage
    TL->>SBE: Execute() — waiting for message
    TL->>SBT: Execute()
    SBT->>SB: SendMessageAsync(message)
    SB-->>SBE: Message received (matching filter)
    SBE-->>TL: ServiceBusReceivedMessage

    Note over TL: Cleanup stage
    TL->>CLN: Execute()
    CLN->>SB: DeleteSubscription("temp-xyz")
```

### 6.3 Scenario: InProcess Function App Test

```mermaid
sequenceDiagram
    participant Test as Test method
    participant TL as Timeline
    participant IPT as InProcessHttpFunctionAppTrigger
    participant PROXY as FunctionAppHttpInProcessCallProxy
    participant FUNC as MyFunction.Run()

    TL->>IPT: Execute(sp, vs, as, log, ct)
    IPT->>PROXY: new(serviceProvider, variableStore, httpRequest)
    IPT->>FUNC: action(proxy) → MyFunction.Run(proxy.Request)
    FUNC->>FUNC: business logic
    FUNC-->>IPT: IActionResult (OkObjectResult, ContentResult, JsonResult, …)
    IPT->>IPT: ConvertToHttpResponseMessage(result)
    IPT-->>TL: HttpResponseMessage

    Note over Test: Full breakpoint debugging available
```

### 6.4 Scenario: Loading Configuration

```mermaid
sequenceDiagram
    participant Test as Test method
    participant CI as ConfigInstance
    participant CL as ConfigLoader
    participant DCP as DefaultConfigProvider
    participant JSON as local.testSettings.json

    Test->>CI: ConfigInstance.FromJsonFile("local.testSettings.json")
    Test->>CI: .LoadAzureConfig()
    CI->>CL: Load(configuration, DefaultConfigProvider)
    CL->>DCP: Load~FunctionAppConfig~(config, "MainFunc")
    DCP->>JSON: config["FunctionApp:MainFunc:BaseUrl"]
    DCP-->>CL: FunctionAppConfig { BaseUrl, Code }
    Note over CL: Repeat for all sections / identifiers
    Test->>CI: .BuildServiceProvider()
```

---

## 7. Deployment View

```mermaid
graph TB
    subgraph "Developer machine / CI agent"
        subgraph "Test process (dotnet test)"
            TESTS[MyTests.dll]
            AZ[TestFrameworkAzure.dll]
            CORE[TestFrameworkCore.dll]
            CFG[TestFrameworkConfig.dll]
            SETTINGS["local.testSettings.json"]
        end
    end
    subgraph "Azure Cloud"
        FUNC_APP[Azure Function App]
        STORAGE[Azure Storage Account]
        COSMOS_DB[Azure Cosmos DB]
        SQL_DB[Azure SQL Database]
        SB[Azure Service Bus]
    end
    TESTS --> AZ
    AZ --> CORE
    AZ --> CFG
    CFG --> SETTINGS
    AZ -->|HTTP/HTTPS| FUNC_APP
    AZ -->|Azure SDK| STORAGE
    AZ -->|Azure SDK| COSMOS_DB
    AZ -->|EF Core| SQL_DB
    AZ -->|AMQP| SB
    FUNC_APP -.->|may use| STORAGE
    FUNC_APP -.->|may use| COSMOS_DB
    FUNC_APP -.->|may use| SQL_DB
    FUNC_APP -.->|may use| SB
```

---

## 8. Cross-Cutting Concepts

### 8.1 Identifier Pattern

Every Azure service is addressed by a **strongly typed identifier** with implicit string conversion:

```csharp
public record StorageAccountIdentifier(string Identifier)
{
    public static implicit operator string(StorageAccountIdentifier id) => id.Identifier;
    public static implicit operator StorageAccountIdentifier(string id) => new(id);
}
```

**JSON configuration (`local.testSettings.json`):**

```json
{
  "StorageAccount": {
    "MainStorage": {
      "ConnectionString": "UseDevelopmentStorage=true",
      "BlobContainerName": "test-blobs",
      "TableContainerName": "test-tables"
    }
  },
  "FunctionApp": {
    "MainFunc": {
      "BaseUrl": "https://my-func.azurewebsites.net",
      "Code": "function-key",
      "AdminCode": "admin-master-key"
    }
  },
  "CosmosDb": {
    "MainDb": {
      "ConnectionString": "AccountEndpoint=...;AccountKey=...",
      "DatabaseName": "TestDb",
      "ContainerName": "Items"
    }
  },
  "ServiceBus": {
    "MainSBTopic": {
      "ConnectionString": "Endpoint=sb://...;SharedAccessKey=...",
      "TopicName":  "test-topic",
      "SubscriptionName": "test-sub"
    }
  },
  "SqlDatabase": {
    "MainSql": {
      "ConnectionString": "Server=...;Database=...;",
      "DatabaseName": "TestDb"
    }
  }
}
```

### 8.2 Artifact Lifecycle per Azure Service

| Service | Setup (Describer) | Resolve (Reference) | Deconstruct (Describer) |
|---------|-------------------|---------------------|------------------------|
| **Blob Storage** | `BlobClient.UploadAsync` | `DownloadContentAsync` | `DeleteAsync` |
| **Table Storage** | `TableClient.UpsertEntityAsync` | `GetEntityAsync(pk, rk)` | `DeleteEntityAsync` |
| **Cosmos DB** | `Container.UpsertItemAsync` | `ReadItemStreamAsync(id, pk)` | `DeleteItemAsync(id, pk)` |
| **SQL Server** | `DbContext.Add/Update` + `SaveChangesAsync` | `FindAsync(keys)` | `Remove` + `SaveChangesAsync` |

### 8.3 Function App — Three Trigger Modes Compared

| Aspect | Remote HTTP | Managed | InProcess |
|--------|-------------|---------|-----------|
| **Target** | Deployed function via HTTP | Deployed function via admin API | Function class in test process |
| **Auth** | Function Key (`Code`) | Admin Master Key (`AdminCode`) | None (direct call) |
| **Return** | `HttpResponseMessage` | `ManagedResult` | `HttpResponseMessage` |
| **Ping check** | Optional | Optional | Not needed |
| **Debugging** | Not possible | Not possible | Full breakpoint debugging |
| **Prerequisite** | Deployed + online | Deployed + online + AdminCode | Project reference to FunctionApp |
| **Builder** | `AzureTF.Trigger.FunctionApp.Http(id)` | `AzureTF.Trigger.FunctionApp.Managed<T>(id, method)` | `AzureTF.Trigger.FunctionApp.InProcessHttp<T>(action)` |

### 8.4 Supported IActionResult Types (InProcess)

`ConvertToHttpResponseMessage` handles:

| IActionResult type | Behaviour |
|-------------------|-----------|
| `null` | 200 OK, empty body |
| `ObjectResult` (incl. `OkObjectResult`, `BadRequestObjectResult`, …) | Status from `StatusCode`, body from `obj.Value.ToString()` |
| `ContentResult` | Status, body, and `ContentType` preserved |
| `JsonResult` | Status + JSON-serialised `Value` (`application/json`) |
| `StatusCodeResult` (incl. `OkResult`, `NoContentResult`, …) | Status, no body |
| Any other type | `NotSupportedException` with clear message |

### 8.5 Service Bus Temp Subscription Lifecycle

```mermaid
stateDiagram-v2
    [*] --> PreStep : Timeline run starts
    PreStep --> SubscriptionLive : CreateSubscription (CorrelationFilter + TTL ≥ 1 h)
    SubscriptionLive --> Receiving : Event waiting for message
    Receiving --> Matched : Message received and filtered
    Matched --> CleanupStep : Timeline run ends
    CleanupStep --> [*] : DeleteSubscription

    Receiving --> TimedOut : Timeout elapsed
    TimedOut --> CleanupStep

    note right of SubscriptionLive
        TTL is max(configuredTimeout, 1 h)
        Subscription auto-deletes if
        the cleanup step never runs
    end note
```

### 8.6 SQL Migration Tracking

```mermaid
stateDiagram-v2
    [*] --> CheckTracker : SqlRowArtifactDescriber.Setup()
    CheckTracker --> AlreadyMigrated : DB already checked this process
    AlreadyMigrated --> Ready

    CheckTracker --> FirstUse : Not yet checked
    FirstUse --> HasMigrations : GetMigrations() returns entries
    HasMigrations --> ApplyMigrations : MigrateAsync()
    FirstUse --> NoMigrations
    NoMigrations --> EnsureCreated : EnsureCreatedAsync()
    ApplyMigrations --> Ready
    EnsureCreated --> Ready

    Ready --> [*] : DbContext ready for CRUD

    note right of FirstUse
        SqlMigrationTracker is static (process-wide).
        Each physical database is checked exactly
        once per process lifetime.
        table-already-exists errors are swallowed
        (SQL Server error 2714 only).
    end note
```

---

## 9. Architecture Decisions

### ADR-1: Static Entry-Point Class Instead of Extension Methods

**Context:** The Azure API must be easy to discover without needing to know which extension methods exist.

**Decision:** `AzureTF` as a static class with nested proxy classes.

**Consequence:** Auto-complete immediately shows `AzureTF.Trigger.*`, `AzureTF.Artifact.*`, etc. Trade-off: not extensible by third parties without modifying the class.

### ADR-2: VariableReference for Dynamic Artifact Identifiers

**Context:** Artifact references (e.g. blob path, table row key) must be resolvable from variables at runtime.

**Decision:** `ArtifactReference` constructors accept `VariableReference<string>` instead of `string`.

**Consequence:** `Var.Const("fixed-path")` for static tests, `Var.Ref<string>("dynamicPath")` for parametrised tests. Artifacts can be named dynamically inside `ForEach` loops.

### ADR-3: 3-Tier SQL DbContext Resolution

**Context:** A test may use multiple SQL databases with different schemas. Identifier → DbContext mapping must be flexible.

**Decision:** 3-tier lookup: (1) explicit registry entries → (2) CLR type name from config → (3) default context.

**Consequence:** Simple tests need only a default context; complex tests can register a context per identifier.

### ADR-4: Pre/Cleanup Steps for Service Bus Subscriptions

**Context:** Temporary subscriptions must be created before a message arrives and deleted afterwards.

**Decision:** `ServiceBusProcessEvent` implements `IHasPreStep` and `IHasCleanupStep`. Core's preprocessor automatically inserts these steps into the pre-setup and cleanup stages.

**Consequence:** Test developers don't manage subscription lifecycle — it's handled automatically.

### ADR-5: InProcess Trigger via Proxy Pattern

**Context:** InProcess tests need access to DI services and variables without starting the Azure Functions host.

**Decision:** `FunctionAppHttpInProcessCallProxy` provides `HttpRequest`, `GetService()`, and `GetVariable()`. The function class is instantiated directly.

**Consequence:** Full debugging support; only HTTP triggers are currently supported in-process. Timer InProcess is also available.

---

## 10. Quality Requirements

### 10.1 Quality Tree

```mermaid
mindmap
  root((Quality))
    Reliability
      Automatic artifact cleanup
      Temp subscriptions with TTL as safety net
      SQL migration tracking
    Maintainability
      One artifact triple per service
      Identifier pattern is extensible
      IConfigProvider is swappable
    Usability
      AzureTF fluent API
      Staged builder for Functions
      JSON configuration
    Testability
      InProcess trigger for debugging
      VariableReference for parametrisation
      ArtifactFinder for query validation
```

### 10.2 Quality Scenarios

| Scenario | Goal | Expected behaviour |
|----------|------|--------------------|
| Write new Cosmos test | Usability | `AzureTF.Artifact.DB.CosmosRef<T>(…)` returns type-safe ref; describer handles upsert/delete |
| Test blob not cleaned up | Reliability | Cleanup stage calls `BlobClient.DeleteAsync()` for every artifact that was set up |
| Add new SQL database | Maintainability | Only add a config section entry; optionally register a DbContext |
| Service Bus test interferes | Testability | Temp subscription with CorrelationId filter isolates the test |
| Function App offline | Reliability | Ping fails → `FunctionAppPingException` with clear message |
| InProcess debugging needed | Testability | `InProcessHttp<T>` allows breakpoints inside function logic |

---

## 11. Risks and Technical Debt

| # | Risk / Debt | Impact | Mitigation |
|---|------------|--------|-----------|
| 1 | **SQL type conversion uses `Convert.ChangeType`** for composite PK values | May fail for non-standard types | Validated against EF Core key types; add guards with informative errors if needed |
| 2 | **Cosmos default resolver uses reflection** (searches for "id"/"partitionKey" properties case-insensitively) | Silent failure if domain model uses non-standard naming | Implement `ICosmosDbIdentifierResolver` to override; document naming convention |
| 3 | **InProcess `IActionResult` limited to 4 types** | Other result types (Redirect, File, …) throw `NotSupportedException` | Add further types on demand |
| 4 | **Service Bus TTL = max(timeout, 1 h)** | Subscription lives up to 1 h even if timeout is shorter | Intentional safety net; auto-delete on idle also set to 2×TTL |
| 5 | **No Queue artifact** for Storage Account | Only Blob and Table covered | Implement as a new artifact triple when needed |
| 6 | **No Event Hub / Event Grid integration** | Only Service Bus covered | Implement as a new artifact triple / async event when needed |

---

## 12. Glossary

| Term | Definition |
|------|-----------|
| **AzureTF** | Static entry-point class for all Azure operations |
| **Identifier** | Strongly typed name for an Azure service instance (e.g. `"MainStorage"`) — maps to a JSON config section |
| **ConfigStore\<T\>** | Dictionary of configs for one service type, keyed by identifier |
| **Trigger** | Step that initiates an action in Azure (Function HTTP call, send Service Bus message) |
| **Artifact** | Managed data object in Azure — automatic setup (create) and deconstruct (delete) |
| **ArtifactFinder** | Query logic for finding artifacts (Cosmos SQL, EF Core LINQ, OData filter) |
| **Event** | Step that waits for an asynchronous external event (e.g. receive Service Bus message) |
| **Temp Subscription** | One-off Service Bus topic subscription with server-side filter, auto-deleted after test |
| **InProcess trigger** | Function invocation directly in the test process — no deployment needed, full debugging |
| **Managed trigger** | Function invocation via admin endpoint (POST /admin/functions/{name}) |
| **Remote HTTP trigger** | Function invocation via the normal HTTP endpoint with a function key |
| **DbContext Registry** | Maps `SqlDatabaseIdentifier` → EF Core `DbContext` type |
| **MigrationTracker** | Process-wide static tracker ensuring each database is migrated exactly once |
| **CRTP** | Curiously Recurring Template Pattern — from Core, enforces type-safe artifact triples |
| **ManagedResult** | Return type of `ManagedRemoteFunctionAppTrigger`: `StatusCode` + `Body` |

---

## 13. Quickstart

### 13.1 Prerequisites

**Project references** in your test project:

```xml
<ProjectReference Include="..\TestFrameworkCore\TestFrameworkCore.csproj" />
<ProjectReference Include="..\TestFrameworkAzure\TestFrameworkAzure.csproj" />
<ProjectReference Include="..\TestFrameworkConfig\TestFrameworkConfig.csproj" />
```

**`local.testSettings.json`** (set as Content / Copy to Output):

```json
{
  "StorageAccount": {
    "MainStorage": {
      "ConnectionString": "UseDevelopmentStorage=true",
      "BlobContainerName": "test-blobs",
      "TableContainerName": "test-tables"
    }
  },
  "FunctionApp": {
    "MainFunc": {
      "BaseUrl": "http://localhost:7071",
      "Code": ""
    }
  },
  "CosmosDb": {
    "MainDb": {
      "ConnectionString": "AccountEndpoint=https://localhost:8081;AccountKey=...",
      "DatabaseName": "TestDb",
      "ContainerName": "Items"
    }
  },
  "ServiceBus": {
    "MainSBTopic": {
      "ConnectionString": "Endpoint=sb://...;SharedAccessKeyName=...;SharedAccessKey=...",
      "TopicName": "test-topic",
      "SubscriptionName": "test-sub"
    }
  },
  "SqlDatabase": {
    "MainSql": {
      "ConnectionString": "Server=localhost;Database=TestDb;Trusted_Connection=true;",
      "DatabaseName": "TestDb"
    }
  }
}
```

Azure resources must exist before running tests (Storage Account, Service Bus Namespace, Cosmos DB, SQL DB, Function App).

### 13.2 Load Configuration

```csharp
using TestFramework.Config;
using TestFrameworkAzure.Extensions;

var serviceProvider = ConfigInstance
    .FromJsonFile("local.testSettings.json")
    .LoadAzureConfig()
    .BuildServiceProvider();
```

### 13.3 Blob Upload Test

```csharp
[Fact]
public async Task Blob_Upload_And_Verify()
{
    var blobRef  = AzureTF.Artifact.StorageAccount.BlobRef("MainStorage", Var.Const("test/sample.json"));
    var blobData = new StorageAccountBlobArtifactData(
        Encoding.UTF8.GetBytes("{\"key\": \"value\"}"),
        new Dictionary<string, string> { ["version"] = "1.0" });

    var timeline = Timeline.Create("BlobTest")
        .SetupArtifact("testBlob")
        .CaptureArtifactVersion("testBlob")
        .Build();

    var run = await timeline
        .SetupRun(serviceProvider, _output)
        .AddArtifact("testBlob", blobRef, blobData)
        .RunAsync();

    run.EnsureRanToCompletion();
    run.Artifact<StorageAccountBlobArtifactData>("testBlob").Should().HaveBeenSetUp();
}
```

### 13.4 Function App HTTP Trigger

```csharp
[Fact]
public async Task Function_HTTP_Trigger()
{
    var trigger = AzureTF.Trigger.FunctionApp.Http("MainFunc")
        .SelectEndpoint("/api/process", HttpMethod.Post)
        .WithBody("{\"input\": \"test\"}")
        .Call();

    var timeline = Timeline.Create("FuncTest")
        .Trigger(trigger)
            .WithTimeOut(TimeSpan.FromSeconds(60))
            .WithRetry(2, CalcDelays.Linear(TimeSpan.FromSeconds(3)))
        .Build();

    var run = await timeline.SetupRun(serviceProvider, _output).RunAsync();
    run.EnsureRanToCompletion();
}
```

### 13.5 Service Bus Send + Receive

```csharp
[Fact]
public async Task ServiceBus_Send_Receive()
{
    var correlationId = Guid.NewGuid().ToString();

    var timeline = Timeline.Create("SBTest")
        .WaitForEvent(AzureTF.Event.ServiceBus.MessageReceived(
            "MainSBTopic",
            correlationId: correlationId,
            createTempSubscription: true,
            completeMessage: true))
            .WithTimeOut(TimeSpan.FromMinutes(2))
        .Trigger(AzureTF.Trigger.ServiceBus.Send("MainSBTopic",
            new ServiceBusMessage("payload") { CorrelationId = correlationId }))
        .Build();

    var run = await timeline.SetupRun(serviceProvider, _output).RunAsync();
    run.EnsureRanToCompletion();
}
```

### 13.6 Cosmos DB Artifact

```csharp
public record MyItem(string id, string partitionKey, string Name);

[Fact]
public async Task Cosmos_Item_Lifecycle()
{
    var itemId    = Guid.NewGuid().ToString();
    var cosmosRef = AzureTF.Artifact.DB.CosmosRef<MyItem>("MainDb", itemId, itemId);
    var cosmosData = new CosmosDbItemArtifactData<MyItem>(new MyItem(itemId, itemId, "TestItem"));

    var timeline = Timeline.Create("CosmosTest")
        .SetupArtifact("myDoc")
        .CaptureArtifactVersion("myDoc")
        .Build();

    var run = await timeline
        .SetupRun(serviceProvider, _output)
        .AddArtifact("myDoc", cosmosRef, cosmosData)
        .RunAsync();

    run.EnsureRanToCompletion();
    // Item is automatically upserted on setup and deleted in cleanup
}
```

### 13.7 SQL Server with EF Core

```csharp
// 1. Register DbContext once (e.g. in test fixture)
services.AddSqlArtifactContexts(registry =>
{
    registry.RegisterDefault<TestDbContext>();
    // or: registry.Register("MainSql", typeof(TestDbContext));
});

// 2. Use in a test
var sqlRef  = AzureTF.Artifact.DB.SqlRef<OrderEntity>("MainSql", orderId.ToString());
var sqlData = new SqlRowArtifactData<OrderEntity>(new OrderEntity { Id = orderId, Name = "TestOrder" });

var timeline = Timeline.Create("SqlTest")
    .SetupArtifact("order")
    .Trigger(myBusinessStep)
    .CaptureArtifactVersion("order")
    .Build();
```

### 13.8 InProcess Function App (Local Debugging)

```csharp
// Requires a project reference to the FunctionApp project

[Fact]
public async Task Function_InProcess_Debug()
{
    var trigger = AzureTF.Trigger.FunctionApp
        .InProcessHttp<MyFunction>(async proxy =>
        {
            var func = new MyFunction(proxy.GetService<IMyService>()!);
            return await func.Run(proxy.Request);
        });

    var timeline = Timeline.Create("InProcessTest")
        .Trigger(trigger).WithTimeOut(TimeSpan.FromSeconds(30))
        .Build();

    var run = await timeline.SetupRun(serviceProvider, _output).RunAsync();
    run.EnsureRanToCompletion();
}
```

### 13.9 End-to-End Test (Blob → Function → Cosmos)

```csharp
[Fact]
public async Task E2E_Blob_Function_Cosmos()
{
    var blobPath = $"input/{Guid.NewGuid()}.json";

    var timeline = Timeline.Create("E2E")
        .SetupArtifact("inputBlob")
        .Trigger(AzureTF.Trigger.FunctionApp.Http("MainFunc")
            .SelectEndpoint($"/api/process?blob={blobPath}", HttpMethod.Post)
            .Call())
            .WithTimeOut(TimeSpan.FromMinutes(2))
            .WithRetry(3, CalcDelays.Exponential)
        .RegisterArtifact("resultDoc", cosmosRef)
        .CaptureArtifactVersion("resultDoc")
        .Build();

    var run = await timeline
        .SetupRun(serviceProvider, _output)
        .AddArtifact("inputBlob", blobRef, blobData)
        .RunAsync();

    run.EnsureRanToCompletion();
    run.Artifact<CosmosDbItemArtifactData<ResultItem>>("resultDoc")
        .Select(d => d.Item.Status)
        .Should().Be("Processed");
}
```
