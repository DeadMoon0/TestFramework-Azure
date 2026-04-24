using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.DB.SqlServer;
using TestFramework.Azure.FunctionApp;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Trigger.IsLive;

public class IsLiveTrigger
{
    public Step<object?> FunctionApp(FunctionAppIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => FunctionApp(identifier, Var.Const(alivenessLevel));

    public Step<object?> FunctionApp(FunctionAppIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new FunctionAppIsLiveTrigger(identifier, alivenessLevel);

    public Step<object?> ServiceBus(ServiceBusIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => ServiceBus(identifier, Var.Const(alivenessLevel));

    public Step<object?> ServiceBus(ServiceBusIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new ServiceBusIsLiveTrigger(identifier, alivenessLevel);

    public Step<object?> Blob(StorageAccountIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => Blob(identifier, Var.Const(alivenessLevel));

    public Step<object?> Blob(StorageAccountIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new BlobStorageIsLiveTrigger(identifier, alivenessLevel);

    public Step<object?> Table(StorageAccountIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => Table(identifier, Var.Const(alivenessLevel));

    public Step<object?> Table(StorageAccountIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new TableStorageIsLiveTrigger(identifier, alivenessLevel);

    public Step<object?> Cosmos(CosmosContainerIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => Cosmos(identifier, Var.Const(alivenessLevel));

    public Step<object?> Cosmos(CosmosContainerIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new CosmosContainerIsLiveTrigger(identifier, alivenessLevel);

    public Step<object?> Sql(SqlDatabaseIdentifier identifier, AlivenessLevel alivenessLevel = AlivenessLevel.Resource) => Sql(identifier, Var.Const(alivenessLevel));

    public Step<object?> Sql(SqlDatabaseIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) => new SqlDatabaseIsLiveTrigger(identifier, alivenessLevel);
}

internal abstract class AzureIsLiveTriggerBase(VariableReference<AlivenessLevel> alivenessLevel) : Step<object?>
{
    public override bool DoesReturn => false;

    protected VariableReference<AlivenessLevel> AlivenessLevelReference { get; } = alivenessLevel;

    protected AlivenessLevel GetAlivenessLevel(VariableStore variableStore) => AlivenessLevelReference.GetValue(variableStore);

    public override void DeclareIO(StepIOContract contract)
    {
        if (AlivenessLevelReference.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(AlivenessLevelReference.Identifier!.Identifier, StepIOKind.Variable, false, typeof(AlivenessLevel)));
    }

    public override StepInstance<Step<object?>, object?> GetInstance() =>
        new StepInstance<Step<object?>, object?>(this);
}

internal sealed class FunctionAppIsLiveTrigger(FunctionAppIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(alivenessLevel), IHasEnvironmentRequirements
{
    private static readonly Uri HostStatusUri = new("admin/host/status", UriKind.Relative);

    public override string Name => "FunctionApp IsLive Trigger";

    public override string Description => "Checks whether the Function App is reachable and accepts the configured key.";

    public override Step<object?> Clone() => new FunctionAppIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        AlivenessLevel currentAlivenessLevel = GetAlivenessLevel(variableStore);
        FunctionAppTriggerConfig triggerConfig = serviceProvider.GetService<FunctionAppTriggerConfig>() ?? new FunctionAppTriggerConfig();
        FunctionAppConfig functionConfig = serviceProvider.GetRequiredService<ConfigStore<FunctionAppConfig>>().GetConfig(identifier);
        RemoteConnection remoteConnection = new(new Uri(functionConfig.BaseUrl));
        if (currentAlivenessLevel == AlivenessLevel.Reachable)
        {
            await remoteConnection.EnsurePingAsync();
            return null;
        }

        if (triggerConfig.DoPing)
        {
            await remoteConnection.EnsurePingAsync();
        }

        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(remoteConnection.BasePath, HostStatusUri));
        request.Headers.Add("x-functions-key", functionConfig.AdminCode ?? functionConfig.Code);

        IHttpRequestSender sender = serviceProvider.GetAzureComponentFactory().Http.CreateSender();
        using HttpResponseMessage response = await sender.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return null;
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.FunctionApp, identifier)];
}

internal sealed class ServiceBusIsLiveTrigger(ServiceBusIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(alivenessLevel), IHasEnvironmentRequirements
{
    public override string Name => "ServiceBus IsLive Trigger";

    public override string Description => "Checks whether the configured Service Bus entity is reachable with the configured credentials.";

    public override Step<object?> Clone() => new ServiceBusIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        AlivenessLevel currentAlivenessLevel = GetAlivenessLevel(variableStore);
        ServiceBusConfig config = serviceProvider.GetRequiredService<ConfigStore<ServiceBusConfig>>().GetConfig(identifier);
        IServiceBusAdministrationAdapter admin = serviceProvider.GetAzureComponentFactory().ServiceBus.CreateAdministration(config);
        if (currentAlivenessLevel == AlivenessLevel.Resource)
        {
            await admin.ValidateConnectionAsync(cancellationToken);
            return null;
        }

        await admin.ValidateNamespaceConnectionAsync(cancellationToken);
        return null;
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.ServiceBus, identifier)];
}

internal sealed class BlobStorageIsLiveTrigger(StorageAccountIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(alivenessLevel), IHasEnvironmentRequirements
{
    public override string Name => "Blob Storage IsLive Trigger";

    public override string Description => "Checks whether the configured Blob container is reachable with the configured credentials.";

    public override Step<object?> Clone() => new BlobStorageIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        AlivenessLevel currentAlivenessLevel = GetAlivenessLevel(variableStore);
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(identifier);
        IBlobContainerAdapter container = serviceProvider.GetAzureComponentFactory().Blob.CreateContainer(config);
        if (currentAlivenessLevel == AlivenessLevel.Resource)
        {
            await container.ValidateConnectionAsync(cancellationToken);
            return null;
        }

        await container.ValidateServiceConnectionAsync(cancellationToken);
        return null;
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.Storage, identifier)];
}

internal sealed class TableStorageIsLiveTrigger(StorageAccountIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(alivenessLevel), IHasEnvironmentRequirements
{
    public override string Name => "Table Storage IsLive Trigger";

    public override string Description => "Checks whether the configured Table storage table is reachable with the configured credentials.";

    public override Step<object?> Clone() => new TableStorageIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        AlivenessLevel currentAlivenessLevel = GetAlivenessLevel(variableStore);
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(identifier);
        ITableAdapter table = serviceProvider.GetAzureComponentFactory().Table.CreateTable(config, config.TableContainerNameRequired);
        if (currentAlivenessLevel == AlivenessLevel.Resource)
        {
            await table.ValidateConnectionAsync(cancellationToken);
            return null;
        }

        await table.ValidateServiceConnectionAsync(cancellationToken);
        return null;
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.Storage, identifier)];
}

internal sealed class CosmosContainerIsLiveTrigger(CosmosContainerIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(alivenessLevel), IHasEnvironmentRequirements
{
    public override string Name => "Cosmos Container IsLive Trigger";

    public override string Description => "Checks whether the configured Cosmos container is reachable with the configured credentials.";

    public override Step<object?> Clone() => new CosmosContainerIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        AlivenessLevel currentAlivenessLevel = GetAlivenessLevel(variableStore);
        CosmosContainerDbConfig config = serviceProvider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig(identifier);
        TimeSpan timeout = TimeOutOptions.TimeOut.GetValue(variableStore);
        if (timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"Cosmos IsLive timeout for '{identifier}' must be greater than zero.");
        }

        ICosmosContainerAdapter container = serviceProvider.GetAzureComponentFactory().Cosmos.CreateContainer(config);

        return currentAlivenessLevel switch
        {
            AlivenessLevel.Reachable => await ExecuteWithTimeoutAsync(
                timeout,
                cancellationToken,
                token => container.ValidateAccountReachabilityAsync(token),
                $"Cosmos account endpoint for '{identifier}' did not respond within {timeout}. Configure the timeline step timeout, for example with .WithTimeOut(...), for slower local or emulator environments."),
            AlivenessLevel.Authenticated => await ExecuteWithTimeoutAsync(
                timeout,
                cancellationToken,
                token => container.ValidateAccountConnectionAsync(token),
                $"Cosmos account for '{identifier}' did not authenticate within {timeout}. Configure the timeline step timeout, for example with .WithTimeOut(...), for slower local or emulator environments."),
            _ => await ExecuteWithTimeoutAsync(
                timeout,
                cancellationToken,
                token => container.ValidateConnectionAsync(token),
                $"Cosmos container '{identifier}' did not respond within {timeout}. Configure the timeline step timeout, for example with .WithTimeOut(...), for slower local or emulator environments."),
        };
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.Cosmos, identifier)];

    private static async Task<object?> ExecuteWithTimeoutAsync(TimeSpan timeout, CancellationToken cancellationToken, Func<CancellationToken, Task> action, string timeoutMessage)
    {
        using CancellationTokenSource timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutTokenSource.CancelAfter(timeout);

        try
        {
            await action(timeoutTokenSource.Token).WaitAsync(timeout, cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && timeoutTokenSource.IsCancellationRequested)
        {
            throw new TimeoutException(timeoutMessage, exception);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(timeoutMessage, exception);
        }

        return null;
    }
}

internal sealed class SqlDatabaseIsLiveTrigger(SqlDatabaseIdentifier identifier, VariableReference<AlivenessLevel> alivenessLevel) : AzureIsLiveTriggerBase(alivenessLevel), IHasEnvironmentRequirements
{
    public override string Name => "SqlDatabase IsLive Trigger";

    public override string Description => "Checks whether the configured SQL database is reachable with the configured credentials.";

    public override Step<object?> Clone() => new SqlDatabaseIsLiveTrigger(identifier, AlivenessLevelReference).WithClonedOptions(this);

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ISqlDbContextResolver resolver = serviceProvider.GetRequiredService<ISqlDbContextResolver>();
        DbContext context = resolver.Resolve(identifier);
        if (!await context.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Could not connect to SQL database '{identifier}'.");
        }

        return null;
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.Sql, identifier)];
}