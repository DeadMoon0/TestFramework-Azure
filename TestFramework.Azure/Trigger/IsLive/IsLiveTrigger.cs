using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.DB.SqlServer;
using TestFramework.Azure.FunctionApp;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.LogicApp;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Trigger.IsLive;

/// <summary>
/// Creates liveness triggers for supported Azure resource types.
/// </summary>
public class IsLiveTrigger
{
    /// <summary>
    /// Creates a Logic App liveness trigger using a constant aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> LogicApp(LogicAppIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => LogicApp(identifier, Var.Const(alivenessLevel));

    /// <summary>
    /// Creates a Logic App liveness trigger using a variable aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> LogicApp(LogicAppIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new LogicAppIsLiveTrigger(identifier, alivenessLevel);

    /// <summary>
    /// Creates a Function App liveness trigger using a constant aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> FunctionApp(FunctionAppIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => FunctionApp(identifier, Var.Const(alivenessLevel));

    /// <summary>
    /// Creates a Function App liveness trigger using a variable aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> FunctionApp(FunctionAppIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new FunctionAppIsLiveTrigger(identifier, alivenessLevel);

    /// <summary>
    /// Creates a Service Bus liveness trigger using a constant aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> ServiceBus(ServiceBusIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => ServiceBus(identifier, Var.Const(alivenessLevel));

    /// <summary>
    /// Creates a Service Bus liveness trigger using a variable aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> ServiceBus(ServiceBusIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new ServiceBusIsLiveTrigger(identifier, alivenessLevel);

    /// <summary>
    /// Creates a Blob Storage liveness trigger using a constant aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> Blob(StorageAccountIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => Blob(identifier, Var.Const(alivenessLevel));

    /// <summary>
    /// Creates a Blob Storage liveness trigger using a variable aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> Blob(StorageAccountIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new BlobStorageIsLiveTrigger(identifier, alivenessLevel);

    /// <summary>
    /// Creates a Table Storage liveness trigger using a constant aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> Table(StorageAccountIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => Table(identifier, Var.Const(alivenessLevel));

    /// <summary>
    /// Creates a Table Storage liveness trigger using a variable aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> Table(StorageAccountIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new TableStorageIsLiveTrigger(identifier, alivenessLevel);

    /// <summary>
    /// Creates a Cosmos DB liveness trigger using a constant aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> Cosmos(CosmosContainerIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => Cosmos(identifier, Var.Const(alivenessLevel));

    /// <summary>
    /// Creates a Cosmos DB liveness trigger using a variable aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> Cosmos(CosmosContainerIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new CosmosContainerIsLiveTrigger(identifier, alivenessLevel);

    /// <summary>
    /// Creates a SQL database liveness trigger using a constant aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> Sql(SqlDatabaseIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => Sql(identifier, Var.Const(alivenessLevel));

    /// <summary>
    /// Creates a SQL database liveness trigger using a variable aliveness level.
    /// </summary>
    public Step<AzureIsLiveResult> Sql(SqlDatabaseIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new SqlDatabaseIsLiveTrigger(identifier, alivenessLevel);
}

internal abstract class AzureIsLiveTriggerBase(string resourceKind, string resourceIdentifier, VariableReference<AlivenessLevel> alivenessLevel) : Step<AzureIsLiveResult>
{
    public override StepExecutionPhase Phase => StepExecutionPhase.Observe;

    public override bool DoesReturn => true;

    protected string ResourceKind { get; } = resourceKind;

    protected string ResourceIdentifier { get; } = resourceIdentifier;

    protected VariableReference<AlivenessLevel> AlivenessLevelReference { get; } = alivenessLevel;

    protected AlivenessLevel GetAlivenessLevel(VariableStore variableStore) => AlivenessLevelReference.GetValue(variableStore);

    protected AzureIsLiveResult CreateResult(VariableStore variableStore, HttpStatusCode? statusCode = null)
        => new(ResourceKind, ResourceIdentifier, GetAlivenessLevel(variableStore), true, statusCode);

    public override void DeclareIO(StepIOContract contract)
    {
        contract.OptionalInput(AlivenessLevelReference);
    }

    public override StepInstance<Step<AzureIsLiveResult>, AzureIsLiveResult> GetInstance() =>
        new StepInstance<Step<AzureIsLiveResult>, AzureIsLiveResult>(this);
}

internal sealed class FunctionAppIsLiveTrigger(FunctionAppIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(AzureEnvironmentResourceKinds.FunctionApp, identifier, alivenessLevel), IHasEnvironmentRequirements
{
    private static readonly Uri HostStatusUri = new("admin/host/status", UriKind.Relative);

    public override string Name => "FunctionApp IsLive Trigger";

    public override string Description => "Checks whether the Function App is reachable and accepts the configured key.";

    public override Step<AzureIsLiveResult> Clone() => new FunctionAppIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<AzureIsLiveResult?> Execute(RunContext context)
    {
        AlivenessLevel currentAlivenessLevel = GetAlivenessLevel(context.Variables);
        FunctionAppTriggerConfig triggerConfig = context.Services.GetService<FunctionAppTriggerConfig>() ?? new FunctionAppTriggerConfig();
        FunctionAppConfig functionConfig = context.Configured<FunctionAppConfig>(identifier);
        RemoteConnection remoteConnection = new(new Uri(functionConfig.BaseUrl));
        if (currentAlivenessLevel == AlivenessLevel.Reachable)
        {
            context.Logger.LogInformation("FunctionApp IsLive '{0}' using reachable probe against '{1}'.", identifier, remoteConnection.BasePath);
            await remoteConnection.EnsureReachableAsync(
                context.Services.GetAzureComponentFactory().Http.CreateSender(),
                triggerConfig.ReachabilityProbeTimeout,
                context.Deadline.Token);
            context.Logger.LogInformation("FunctionApp IsLive '{0}' reachable probe succeeded.", identifier);
            return CreateResult(context.Variables);
        }

        if (triggerConfig.DoPing)
        {
            context.Logger.LogInformation("FunctionApp IsLive '{0}' pre-pinging '{1}' before authenticated host-status probe.", identifier, remoteConnection.BasePath);
            await remoteConnection.EnsurePingAsync(triggerConfig.ReachabilityProbeTimeout, context.Deadline.Token);
        }

        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(remoteConnection.BasePath, HostStatusUri));
        request.Headers.Add("x-functions-key", functionConfig.AdminCode ?? functionConfig.Code);
        context.Logger.LogInformation("FunctionApp IsLive '{0}' probing host status at '{1}'.", identifier, request.RequestUri?.ToString() ?? "(unknown)");

        IHttpRequestSender sender = context.Services.GetAzureComponentFactory().Http.CreateSender();
        using HttpResponseMessage response = await sender.SendAsync(request, context.Deadline.Token);
        response.EnsureSuccessStatusCode();
        context.Logger.LogInformation("FunctionApp IsLive '{0}' returned status {1}.", identifier, response.StatusCode);
        return CreateResult(context.Variables, response.StatusCode);
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.FunctionApp, identifier)];
}

internal sealed class LogicAppIsLiveTrigger(LogicAppIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(AzureEnvironmentResourceKinds.LogicApp, identifier, alivenessLevel), IHasEnvironmentRequirements
{
    private static readonly Uri HostStatusUri = new("admin/host/status", UriKind.Relative);

    public override string Name => "LogicApp IsLive Trigger";

    public override string Description => "Checks whether the Logic App host is reachable and accepts the configured key.";

    public override Step<AzureIsLiveResult> Clone() => new LogicAppIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<AzureIsLiveResult?> Execute(RunContext context)
    {
        AlivenessLevel currentAlivenessLevel = GetAlivenessLevel(context.Variables);
        LogicAppConfig logicAppConfig = context.Configured<LogicAppConfig>(identifier);
        Uri probeBaseUri = logicAppConfig.HostingMode == LogicAppHostingMode.Standard
            ? new Uri(LogicAppRoutes.RequireStandardBaseUrl(logicAppConfig))
            : LogicAppRoutes.BuildIsLiveProbeUri(logicAppConfig, logicAppConfig.WorkflowName);
        RemoteConnection remoteConnection = new(probeBaseUri);
        if (currentAlivenessLevel == AlivenessLevel.Reachable)
        {
            context.Logger.LogInformation("LogicApp IsLive '{0}' using reachable probe against '{1}' in {2} mode.", identifier, remoteConnection.DisplayPath, logicAppConfig.HostingMode);
            FunctionAppTriggerConfig probeConfig = context.Services.GetService<FunctionAppTriggerConfig>() ?? new FunctionAppTriggerConfig();
            await remoteConnection.EnsureReachableAsync(
                context.Services.GetAzureComponentFactory().Http.CreateSender(),
                probeConfig.ReachabilityProbeTimeout,
                context.Deadline.Token);
            context.Logger.LogInformation("LogicApp IsLive '{0}' reachable probe succeeded.", identifier);
            return CreateResult(context.Variables);
        }

        using HttpRequestMessage request = logicAppConfig.HostingMode == LogicAppHostingMode.Standard
            ? new HttpRequestMessage(HttpMethod.Get, new Uri(remoteConnection.BasePath, HostStatusUri))
            : new HttpRequestMessage(HttpMethod.Get, LogicAppRoutes.BuildIsLiveProbeUri(logicAppConfig, logicAppConfig.WorkflowName));

        if (logicAppConfig.HostingMode == LogicAppHostingMode.Standard)
        {
            string hostKey = logicAppConfig.Standard.AdminCode ?? logicAppConfig.Standard.Code ?? throw new TestFramework.Core.Exceptions.FrameworkConfigurationException($"Logic App '{identifier}' requires Standard:AdminCode or Standard:Code for authenticated liveness checks.");
            request.Headers.Add("x-functions-key", hostKey);
        }
        else if (LogicAppRoutes.HasConsumptionManagementResourceId(logicAppConfig))
        {
            await LogicAppRoutes.AuthorizeManagementRequestAsync(context.Services, identifier, logicAppConfig, request, context.Deadline.Token);
        }

        IHttpRequestSender sender = context.Services.GetAzureComponentFactory().Http.CreateSender();
        context.Logger.LogInformation("LogicApp IsLive '{0}' probing '{1}' in {2} mode.", identifier, LogicAppUriRedaction.Redact(request.RequestUri?.ToString()) ?? "(unknown)", logicAppConfig.HostingMode);
        using HttpResponseMessage response = await sender.SendAsync(request, context.Deadline.Token);
        if (logicAppConfig.HostingMode == LogicAppHostingMode.Consumption)
        {
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                    $"Logic App '{identifier}' rejected the configured Consumption liveness probe. "
                    + "If this host only has invoke access, use reachable-level checks or configure management capability with WorkflowResourceId plus ILogicAppConsumptionManagementRequestAuthorizer.");

            if ((int)response.StatusCode >= 500)
                response.EnsureSuccessStatusCode();

            context.Logger.LogInformation("LogicApp IsLive '{0}' returned status {1}.", identifier, response.StatusCode);
            return CreateResult(context.Variables, response.StatusCode);
        }

        response.EnsureSuccessStatusCode();
        context.Logger.LogInformation("LogicApp IsLive '{0}' returned status {1}.", identifier, response.StatusCode);
        return CreateResult(context.Variables, response.StatusCode);
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.LogicApp, identifier)];
}

internal sealed class ServiceBusIsLiveTrigger(ServiceBusIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(AzureEnvironmentResourceKinds.ServiceBus, identifier, alivenessLevel), IHasEnvironmentRequirements
{
    public override string Name => "ServiceBus IsLive Trigger";

    public override string Description => "Checks whether the configured Service Bus entity is reachable with the configured credentials.";

    public override Step<AzureIsLiveResult> Clone() => new ServiceBusIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<AzureIsLiveResult?> Execute(RunContext context)
    {
        AlivenessLevel currentAlivenessLevel = GetAlivenessLevel(context.Variables);
        IServiceBusAdministrationAdapter admin = context.Services.GetAzureComponentFactory()
            .ServiceBus.CreateAdministration(AzureResources.For<ServiceBusConfig>(context, identifier));
        if (currentAlivenessLevel == AlivenessLevel.Resource)
        {
            await admin.ValidateConnectionAsync(context.Deadline.Token);
            return CreateResult(context.Variables);
        }

        await admin.ValidateNamespaceConnectionAsync(context.Deadline.Token);
        return CreateResult(context.Variables);
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.ServiceBus, identifier)];
}

internal sealed class BlobStorageIsLiveTrigger(StorageAccountIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(AzureEnvironmentResourceKinds.Storage, identifier, alivenessLevel), IHasEnvironmentRequirements
{
    public override string Name => "Blob Storage IsLive Trigger";

    public override string Description => "Checks whether the configured Blob container is reachable with the configured credentials.";

    public override Step<AzureIsLiveResult> Clone() => new BlobStorageIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<AzureIsLiveResult?> Execute(RunContext context)
    {
        AlivenessLevel currentAlivenessLevel = GetAlivenessLevel(context.Variables);
        IBlobContainerAdapter container = context.Services.GetAzureComponentFactory()
            .Blob.CreateContainer(AzureResources.For<StorageAccountConfig>(context, identifier));
        if (currentAlivenessLevel == AlivenessLevel.Resource)
        {
            await container.ValidateConnectionAsync(context.Deadline.Token);
            return CreateResult(context.Variables);
        }

        await container.ValidateServiceConnectionAsync(context.Deadline.Token);
        return CreateResult(context.Variables);
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.Storage, identifier)];
}

internal sealed class TableStorageIsLiveTrigger(StorageAccountIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(AzureEnvironmentResourceKinds.Storage, identifier, alivenessLevel), IHasEnvironmentRequirements
{
    public override string Name => "Table Storage IsLive Trigger";

    public override string Description => "Checks whether the configured Table storage table is reachable with the configured credentials.";

    public override Step<AzureIsLiveResult> Clone() => new TableStorageIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<AzureIsLiveResult?> Execute(RunContext context)
    {
        AlivenessLevel currentAlivenessLevel = GetAlivenessLevel(context.Variables);
        Resolved<StorageAccountConfig> account = AzureResources.For<StorageAccountConfig>(context, identifier);
        ITableAdapter table = context.Services.GetAzureComponentFactory()
            .Table.CreateTable(account, account.Value.TableContainerNameRequired);
        if (currentAlivenessLevel == AlivenessLevel.Resource)
        {
            await table.ValidateConnectionAsync(context.Deadline.Token);
            return CreateResult(context.Variables);
        }

        await table.ValidateServiceConnectionAsync(context.Deadline.Token);
        return CreateResult(context.Variables);
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.Storage, identifier)];
}

internal sealed class CosmosContainerIsLiveTrigger(CosmosContainerIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(AzureEnvironmentResourceKinds.Cosmos, identifier, alivenessLevel), IHasEnvironmentRequirements
{
    public override string Name => "Cosmos Container IsLive Trigger";

    public override string Description => "Checks whether the configured Cosmos container is reachable with the configured credentials.";

    public override Step<AzureIsLiveResult> Clone() => new CosmosContainerIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<AzureIsLiveResult?> Execute(RunContext context)
    {
        AlivenessLevel currentAlivenessLevel = GetAlivenessLevel(context.Variables);
        Resolved<CosmosContainerDbConfig> cosmos = AzureResources.For<CosmosContainerDbConfig>(context, identifier);
        TimeSpan timeout = TimeOutOptions.TimeOut.GetValue(context.Variables);
        if (timeout <= TimeSpan.Zero)
        {
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException($"Cosmos IsLive timeout for '{identifier}' must be greater than zero.");
        }

        ICosmosContainerAdapter container = context.Services.GetAzureComponentFactory().Cosmos.CreateContainer(cosmos);

        return currentAlivenessLevel switch
        {
            AlivenessLevel.Reachable => await ExecuteWithTimeoutAsync(
                context.Variables,
                timeout,
                context.Deadline.Token,
                token => container.ValidateAccountReachabilityAsync(token),
                $"Cosmos account endpoint for '{identifier}' did not respond within {timeout}. Configure the timeline step timeout, for example with .WithTimeOut(...), for slower local or emulator environments."),
            AlivenessLevel.Authenticated => await ExecuteWithTimeoutAsync(
                context.Variables,
                timeout,
                context.Deadline.Token,
                token => container.ValidateAccountConnectionAsync(token),
                $"Cosmos account for '{identifier}' did not authenticate within {timeout}. Configure the timeline step timeout, for example with .WithTimeOut(...), for slower local or emulator environments."),
            _ => await ExecuteWithTimeoutAsync(
                context.Variables,
                timeout,
                context.Deadline.Token,
                token => container.ValidateConnectionAsync(token),
                $"Cosmos container '{identifier}' did not respond within {timeout}. Configure the timeline step timeout, for example with .WithTimeOut(...), for slower local or emulator environments."),
        };
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.Cosmos, identifier)];

    private async Task<AzureIsLiveResult?> ExecuteWithTimeoutAsync(VariableStore variableStore, TimeSpan timeout, CancellationToken cancellationToken, Func<CancellationToken, Task> action, string timeoutMessage)
    {
        using CancellationTokenSource timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutTokenSource.CancelAfter(timeout);

        try
        {
            await action(timeoutTokenSource.Token).WaitAsync(timeout, cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && timeoutTokenSource.IsCancellationRequested)
        {
            throw new TestFramework.Core.Exceptions.FrameworkTimeoutException(timeoutMessage, exception);
        }
        catch (TimeoutException exception)
        {
            throw new TestFramework.Core.Exceptions.FrameworkTimeoutException(timeoutMessage, exception);
        }

        return CreateResult(variableStore);
    }
}

internal sealed class SqlDatabaseIsLiveTrigger(SqlDatabaseIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(AzureEnvironmentResourceKinds.Sql, identifier, alivenessLevel), IHasEnvironmentRequirements
{
    public override string Name => "SqlDatabase IsLive Trigger";

    public override string Description => "Checks whether the configured SQL database is reachable with the configured credentials.";

    public override Step<AzureIsLiveResult> Clone() => new SqlDatabaseIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<AzureIsLiveResult?> Execute(RunContext context)
    {
        ISqlDbContextResolver resolver = context.Services.GetRequiredService<ISqlDbContextResolver>();
        await using DbContext dbContext = resolver.Resolve(context, identifier);
        if (!await dbContext.Database.CanConnectAsync(context.Deadline.Token))
        {
            throw new TestFramework.Core.Exceptions.FrameworkStateException($"Could not connect to SQL database '{identifier}'.");
        }

        return CreateResult(context.Variables);
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.Sql, identifier)];
}