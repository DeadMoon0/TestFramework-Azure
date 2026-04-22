<identity>
    <package>TestFramework.Azure</package>
    <role>addon-skill</role>
</identity>

<objective>
    Explain the Azure-specific capabilities that extend the TestFramework timeline model, including fluent entry points, identifier-driven configuration, messaging flows, Function App usage, and artifact lookups.
</objective>

<package_scope>
    Covers Function Apps, Service Bus, Blob Storage, Table Storage, Cosmos DB, SQL-backed artifacts, and related Azure-focused triggers, events, and identifiers.
</package_scope>

<key_concepts>
    Azure extends the Core timeline engine with cloud-facing triggers, waits, artifacts, and identifiers.
    Most Azure tests still follow the same build, run, and assert pattern from Core.
    Azure scenarios often need configuration and environment identifiers before the timeline can run.
    The package exposes a fluent entry surface through AzureTF for triggers, events, artifact lookup, and environment-specific helpers.
    Configuration is identifier-driven. Timelines refer to names like Default or MainSBQueue, and the package resolves those names to concrete Azure resources through configuration providers.
</key_concepts>

<best_practices>
    Keep Azure timelines readable and explicit about external dependencies.
    Separate environment setup from assertion logic.
    Prefer named steps and explicit identifiers so failures are easy to diagnose.
    Keep cloud-side waits and message flows visible in the timeline, not hidden in helpers.
    Prefer one visible end-to-end flow over several tiny wrappers that hide the Azure interaction sequence.
    Use correlation ids or equivalent scoping when message-driven tests would otherwise be ambiguous.
</best_practices>

<api_hints>
    Important APIs and shapes from the package docs:
    - AzureTF.Trigger.FunctionApp.Http("name").SelectEndpointWithMethod<T>(...).Call()
    - AzureTF.Trigger.ServiceBus.Send("configName", message)
    - AzureTF.Event.ServiceBus.MessageReceived("configName", ...)
    - AzureTF.Trigger.IsLive.Cosmos("configName")
    - AzureTF.ArtifactFinder.DB.CosmosQuery<T>("configName", queryDefinition)
    - Config extensions such as LoadAzureConfig() prepare Azure-specific configuration.

    Operational hint:
    Many Azure APIs depend on named configuration entries like "Default" or "MainSBQueue" rather than raw connection strings in the timeline code.
</api_hints>

<runtime_behavior>
    Important runtime facts:
    - AzureTF is the main facade and groups Trigger, Event, Artifact, and ArtifactFinder entry points.
    - Service Bus receive flows may create temporary subscriptions for scoped topic tests.
    - Most Azure operations rely on configuration stores resolved from dependency injection and IConfiguration.
    - Live checks and remote interactions should be explicit in the timeline so waiting, retries, and timeouts remain visible.
</runtime_behavior>

<config_model>
    Important configuration ideas:
    - identifiers such as FunctionAppIdentifier, ServiceBusIdentifier, CosmosContainerIdentifier, SqlDatabaseIdentifier, and StorageAccountIdentifier are thin named wrappers
    - config loading is done through providers and stores, not direct connection strings inside tests
    - LoadAzureConfig() is the standard preparation path when using TestFramework.Config
    - named config entries are the boundary between the test timeline and the real Azure environment
</config_model>

<style_guide>
    Prefer timelines where the Azure interaction order is obvious at a glance.
    Keep the timeline close to the real distributed flow: send, wait, inspect, assert.
    Use explicit configuration identifiers that describe the environment dependency.
    Name Azure steps when diagnosing message flow or remote calls will matter.
    Keep resource names and identifiers semantically meaningful, because diagnostics often surface those names directly.
</style_guide>

<sample_patterns>
    Function App pattern:
    - prepare Azure config with ConfigInstance
    - trigger a function endpoint through AzureTF.Trigger.FunctionApp.Http(...)
    - run the timeline with the built service provider

    Service Bus pattern:
    - trigger a send
    - wait for MessageReceived with correlation id filtering
    - apply a timeline timeout close to the wait

    Data lookup pattern:
    - use artifact finders for Cosmos or other Azure-backed data retrieval
    - keep query intent visible in the timeline or finder call
</sample_patterns>

<anti_patterns>
    Avoid:
    - hardcoding connection strings or raw environment details in the timeline code
    - using shared queue or topic flows without correlation or temp subscription isolation
    - hiding waits, receives, or lookup steps behind helper abstractions that obscure the distributed flow
    - skipping timeout configuration for event-based waits
</anti_patterns>

<sources>
    TestFramework-Azure/README.md
    TestFramework-Azure/TestFramework.Azure/README.md
    TestFramework-Showroom/TestFramework.Showroom.Azure
</sources>

<grounding_files>
    Most important files for expert grounding:
    - TestFramework-Azure/TestFramework.Azure/AzureTF.cs
    - TestFramework-Azure/TestFramework.Azure/Extensions/ConfigExtension.cs
    - TestFramework-Azure/TestFramework.Azure/Configuration/ConfigLoader.cs
    - TestFramework-Azure/TestFramework.Azure/Configuration/DefaultConfigProvider.cs
    - TestFramework-Azure/TestFramework.Azure/ServiceBus/ServiceBusProcessEvent.cs
    - TestFramework-Azure/UnitTests/TestFramework.Azure.Tests/AzureDslCoverageTests.cs
    - TestFramework-Azure/UnitTests/TestFramework.Azure.Tests/AzureSurfaceTests.cs
</grounding_files>

<repo_resolution>
    Resolve repository metadata with commands when needed:
    dotnet msbuild TestFramework-Azure/TestFramework.Azure/TestFramework.Azure.csproj -getProperty:RepositoryUrl
    dotnet msbuild TestFramework-Azure/TestFramework.Azure/TestFramework.Azure.csproj -getProperty:PackageProjectUrl
</repo_resolution>