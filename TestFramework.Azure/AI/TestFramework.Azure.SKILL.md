<identity>
    <package>TestFramework.Azure</package>
    <role>addon-skill</role>
</identity>

<objective>
    Explain the Azure-specific capabilities that extend the TestFramework timeline model.
</objective>

<package_scope>
    Covers Function Apps, Service Bus, Blob Storage, Table Storage, Cosmos DB, SQL-backed artifacts, and related Azure-focused triggers, events, and identifiers.
</package_scope>

<key_concepts>
    Azure extends the Core timeline engine with cloud-facing triggers, waits, artifacts, and identifiers.
    Most Azure tests still follow the same build, run, and assert pattern from Core.
    Azure scenarios often need configuration and environment identifiers before the timeline can run.
    The package exposes a fluent entry surface through AzureTF for triggers, events, artifact lookup, and environment-specific helpers.
</key_concepts>

<best_practices>
    Keep Azure timelines readable and explicit about external dependencies.
    Separate environment setup from assertion logic.
    Prefer named steps and explicit identifiers so failures are easy to diagnose.
    Keep cloud-side waits and message flows visible in the timeline, not hidden in helpers.
    Prefer one visible end-to-end flow over several tiny wrappers that hide the Azure interaction sequence.
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

<style_guide>
    Prefer timelines where the Azure interaction order is obvious at a glance.
    Keep the timeline close to the real distributed flow: send, wait, inspect, assert.
    Use explicit configuration identifiers that describe the environment dependency.
    Name Azure steps when diagnosing message flow or remote calls will matter.
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

<sources>
    TestFramework-Azure/README.md
    TestFramework-Azure/TestFramework.Azure/README.md
    TestFramework-Showroom/TestFramework.Showroom.Azure
</sources>

<repo_resolution>
    Resolve repository metadata with commands when needed:
    dotnet msbuild TestFramework-Azure/TestFramework.Azure/TestFramework.Azure.csproj -getProperty:RepositoryUrl
    dotnet msbuild TestFramework-Azure/TestFramework.Azure/TestFramework.Azure.csproj -getProperty:PackageProjectUrl
</repo_resolution>