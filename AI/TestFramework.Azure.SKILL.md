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
    The package exposes a fluent entry surface through AzureExt for triggers, events, artifact lookup, and environment-specific helpers.
    Configuration is identifier-driven. Timelines refer to names like Default or MainSBQueue, and the package resolves those names to concrete Azure resources through configuration providers.
</key_concepts>

<best_practices>
    Keep Azure timelines readable and explicit about external dependencies.
    Separate environment setup from assertion logic.
    Prefer named steps and explicit identifiers so failures are easy to diagnose.
    Keep cloud-side waits and message flows visible in the timeline, not hidden in helpers.
    Prefer one visible end-to-end flow over several tiny wrappers that hide the Azure interaction sequence.
    Use correlation ids or equivalent scoping when message-driven tests would otherwise be ambiguous.
    Prefer AzureExt entry points and identifier-driven config over ad-hoc nested helper layers.
    Prefer compact AzureExt call chains when they remain readable, for example `Trigger(AzureExt.Trigger.FunctionApp.Http(...).SelectEndpointWithMethod<...>(...).Call())`.
</best_practices>

<surface_guidance>
    The Azure package is intentionally broad, but the agent should still teach it through a consumer-first path:
    - AzureExt as the main facade
    - named identifiers as the configuration contract
    - visible send, call, wait, and lookup steps in the timeline

    Do not push users deeper into proxy layering unless they are actually extending the package.
    If a scenario looks noisy, simplify the timeline around identifiers, explicit steps, and correlation rather than inventing more wrappers.
</surface_guidance>

<api_hints>
    Important APIs and shapes from the package docs:
    - AzureExt.Trigger.FunctionApp.Http("name").SelectEndpointWithMethod<T>(...).Call()
    - AzureExt.Trigger.LogicApp.Http("logic").Workflow("StatefulWorkflow").Manual().Call()
    - AzureExt.Trigger.LogicApp.Http("logic").Workflow("StatelessWorkflow").Manual().CallAndCapture()
    - AzureExt.Event.LogicApp.RunCompleted("logic", runId, workflowName) is for stateful workflows only
    - AzureExt.Trigger.ServiceBus.Send("configName", message)
    - AzureExt.Event.ServiceBus.MessageReceived("configName", ...)
    - AzureExt.Trigger.IsLive.Cosmos("configName")
    - AzureExt.ArtifactFinder.DB.CosmosQuery<T>("configName", queryDefinition)
    - Config extensions such as LoadAzureConfig() prepare Azure-specific configuration.

    Operational hint:
    Many Azure APIs depend on named configuration entries like "Default" or "MainSBQueue" rather than raw connection strings in the timeline code.
</api_hints>

<runtime_behavior>
    Important runtime facts:
    - AzureExt is the main facade and groups Trigger, Event, Artifact, and ArtifactFinder entry points.
    - Logic App workflow mode matters: stateful workflows expose run history, stateless workflows complete inline with the callback response.
    - Use `Call()` when you need a run id and plan to wait with `RunCompleted(...)` or `RunReachedStatus(...)`.
    - Use `CallAndCapture()` for stateless Logic Apps; do not model stateless completion through run polling or hidden fallback storage.
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

<project_adaptation>
    Adapting Azure config to the user's project:
    - The seam for adapting TestFramework.Azure to a project's existing configuration layout is IConfigProvider.
    - DefaultConfigProvider expects sections like FunctionApp, StorageAccount, CosmosDb, ServiceBus, and SqlDatabase.
    - If the user's project already has its own config naming or section layout, implement a project-specific IConfigProvider that translates the user's layout into TestFramework.Azure config objects.
    - If no project-specific mapping for a field is needed, map that field exactly like the default package behavior would map it.
    - Only override the fields or section names that actually differ from the default contract; do not rewrite the whole provider just because one or two fields are different.
    - The normal integration point is a common shared project that exposes one extension method which calls LoadAzureConfig(customProvider) or services.LoadAzureConfigs(configuration, customProvider).
    - That shared extension should become the single project-level adapter so test code keeps calling one project-specific method instead of repeating provider wiring everywhere.

    Naming guidance:
    - Follow the user's project naming, not the package naming, at the project boundary.
    - A common pattern is a project extension name ending with Transform or similar, to signal that the project config is being transformed into TestFramework.Azure's expected model.
    - Keep the project-specific adapter names stable and descriptive so errors clearly point back to the owning project seam.
</project_adaptation>

<documentation_notes>
    Guidance the agent should preserve:
    - public Azure docs and config guidance are the preferred happy path instead of custom wrapper invention
    - the remaining weaknesses are ergonomic backlog items such as proxy depth and overload cleanup, not reasons to redesign working flows during normal user requests
    - duplicated Function App HTTP request-shaping logic was centralized; when modifying that area, keep remote and in-process request behavior aligned
</documentation_notes>

<style_guide>
    Prefer timelines where the Azure interaction order is obvious at a glance.
    Keep the timeline close to the real distributed flow: send, wait, inspect, assert.
    Use explicit configuration identifiers that describe the environment dependency.
    Name Azure steps when diagnosing message flow or remote calls will matter.
    Keep resource names and identifiers semantically meaningful, because diagnostics often surface those names directly.
    Avoid expanding every method argument across several lines when a compact call reads more like normal C#.
    Keep example domain language concrete and scenario-true instead of generic deployment wording.
</style_guide>

<sample_patterns>
    Function App pattern:
    - prepare Azure config with ConfigInstance
    - trigger a function endpoint through AzureExt.Trigger.FunctionApp.Http(...)
    - run the timeline with the built service provider

    Logic App pattern:
    - stateful workflow: trigger with `Call()`, keep the returned `RunId`, then wait with `AzureExt.Event.LogicApp.RunCompleted(...)`
    - stateless workflow: trigger with `CallAndCapture()` and assert against the returned `LogicAppCapturedResult`
    - do not recommend `RunCompleted(...)` for stateless workflows

    Service Bus pattern:
    - trigger a send
    - wait for MessageReceived with correlation id filtering
    - apply a timeline timeout close to the wait

    Data lookup pattern:
    - use artifact finders for Cosmos or other Azure-backed data retrieval
    - keep query intent visible in the timeline or finder call

    Project adapter pattern:
    - create one shared project extension method in the user's common project
    - inside it, instantiate or resolve the project's custom IConfigProvider
    - call LoadAzureConfig(customProvider) or LoadAzureConfigs(configuration, customProvider)
    - let unchanged fields fall through to the same semantics as DefaultConfigProvider
    - keep raw section-name translation inside that adapter, not in the individual tests
</sample_patterns>

<anti_patterns>
    Avoid:
    - hardcoding connection strings or raw environment details in the timeline code
    - using shared queue or topic flows without correlation or temp subscription isolation
    - hiding waits, receives, or lookup steps behind helper abstractions that obscure the distributed flow
    - skipping timeout configuration for event-based waits
    - forcing the user's project to rename its config layout just to match DefaultConfigProvider
    - scattering custom Azure config translation logic across many test classes instead of one shared adapter extension
    - replacing default field mapping behavior when the project's field can already be interpreted with the package default
</anti_patterns>

<important_type_map>
    Common type map for discovery and error interpretation:
    - AzureExt: main facade for Azure triggers, events, artifact registration, and artifact finding
    - IConfigProvider: project-to-TestFramework.Azure config adapter seam
    - DefaultConfigProvider: default reader for the package's standard section names
    - ConfigLoader: component that loads all Azure configs into DI stores
    - FunctionAppIdentifier / ServiceBusIdentifier / CosmosContainerIdentifier / SqlDatabaseIdentifier / StorageAccountIdentifier: named identifiers that bind test code to configured resources
    - FunctionAppConfig / ServiceBusConfig / CosmosContainerDbConfig / SqlDatabaseConfig / StorageAccountConfig: concrete config records resolved from IConfiguration

    Discovery heuristics for the agent:
    - If users talk about "our config naming", "mapping our settings", or "custom Azure config", they usually need IConfigProvider.
    - If errors mention missing identifiers or unknown config names, inspect the provider implementation and the configured identifier names first.
    - If users mention a shared test project or common project extension, treat that as the correct place for Azure config adaptation.
    - If users mention names ending with Transform, Adapter, or Extension around config loading, inspect those before changing tests.
</important_type_map>

<sources>
    TestFramework-Azure/README.md
    TestFramework-Azure/TestFramework.Azure/README.md
    TestFramework-Showroom/TestFramework.Showroom.Azure
</sources>

<grounding_files>
    Most important files for expert grounding:
    - TestFramework-Azure/TestFramework.Azure/AzureExt.cs
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