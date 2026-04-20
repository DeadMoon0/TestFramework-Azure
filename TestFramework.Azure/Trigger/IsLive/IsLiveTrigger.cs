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
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Trigger.IsLive;

public class IsLiveTrigger
{
    public Step<object?> FunctionApp(FunctionAppIdentifier identifier) => new FunctionAppIsLiveTrigger(identifier);

    public Step<object?> ServiceBus(ServiceBusIdentifier identifier) => new ServiceBusIsLiveTrigger(identifier);

    public Step<object?> Blob(StorageAccountIdentifier identifier) => new BlobStorageIsLiveTrigger(identifier);

    public Step<object?> Table(StorageAccountIdentifier identifier) => new TableStorageIsLiveTrigger(identifier);

    public Step<object?> Cosmos(CosmosContainerIdentifier identifier) => new CosmosContainerIsLiveTrigger(identifier);

    public Step<object?> Sql(SqlDatabaseIdentifier identifier) => new SqlDatabaseIsLiveTrigger(identifier);
}

internal abstract class AzureIsLiveTriggerBase : Step<object?>
{
    public override bool DoesReturn => false;

    public override void DeclareIO(StepIOContract contract) { }

    public override StepInstance<Step<object?>, object?> GetInstance() =>
        new StepInstance<Step<object?>, object?>(this);
}

internal sealed class FunctionAppIsLiveTrigger(FunctionAppIdentifier identifier) : AzureIsLiveTriggerBase
{
    private static readonly Uri HostStatusUri = new("admin/host/status", UriKind.Relative);

    public override string Name => "FunctionApp IsLive Trigger";

    public override string Description => "Checks whether the Function App is reachable and accepts the configured key.";

    public override Step<object?> Clone() => new FunctionAppIsLiveTrigger(identifier).WithClonedOptions(this);

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        FunctionAppTriggerConfig triggerConfig = serviceProvider.GetService<FunctionAppTriggerConfig>() ?? new FunctionAppTriggerConfig();
        FunctionAppConfig functionConfig = serviceProvider.GetRequiredService<ConfigStore<FunctionAppConfig>>().GetConfig(identifier);
        RemoteConnection remoteConnection = new(new Uri(functionConfig.BaseUrl));
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
}

internal sealed class ServiceBusIsLiveTrigger(ServiceBusIdentifier identifier) : AzureIsLiveTriggerBase
{
    public override string Name => "ServiceBus IsLive Trigger";

    public override string Description => "Checks whether the configured Service Bus entity is reachable with the configured credentials.";

    public override Step<object?> Clone() => new ServiceBusIsLiveTrigger(identifier).WithClonedOptions(this);

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ServiceBusConfig config = serviceProvider.GetRequiredService<ConfigStore<ServiceBusConfig>>().GetConfig(identifier);
        IServiceBusAdministrationAdapter admin = serviceProvider.GetAzureComponentFactory().ServiceBus.CreateAdministration(config);
        await admin.ValidateConnectionAsync(cancellationToken);
        return null;
    }
}

internal sealed class BlobStorageIsLiveTrigger(StorageAccountIdentifier identifier) : AzureIsLiveTriggerBase
{
    public override string Name => "Blob Storage IsLive Trigger";

    public override string Description => "Checks whether the configured Blob container is reachable with the configured credentials.";

    public override Step<object?> Clone() => new BlobStorageIsLiveTrigger(identifier).WithClonedOptions(this);

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(identifier);
        IBlobContainerAdapter container = serviceProvider.GetAzureComponentFactory().Blob.CreateContainer(config);
        await container.ValidateConnectionAsync(cancellationToken);
        return null;
    }
}

internal sealed class TableStorageIsLiveTrigger(StorageAccountIdentifier identifier) : AzureIsLiveTriggerBase
{
    public override string Name => "Table Storage IsLive Trigger";

    public override string Description => "Checks whether the configured Table storage table is reachable with the configured credentials.";

    public override Step<object?> Clone() => new TableStorageIsLiveTrigger(identifier).WithClonedOptions(this);

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(identifier);
        ITableAdapter table = serviceProvider.GetAzureComponentFactory().Table.CreateTable(config, config.TableContainerNameRequired);
        await table.ValidateConnectionAsync(cancellationToken);
        return null;
    }
}

internal sealed class CosmosContainerIsLiveTrigger(CosmosContainerIdentifier identifier) : AzureIsLiveTriggerBase
{
    public override string Name => "Cosmos Container IsLive Trigger";

    public override string Description => "Checks whether the configured Cosmos container is reachable with the configured credentials.";

    public override Step<object?> Clone() => new CosmosContainerIsLiveTrigger(identifier).WithClonedOptions(this);

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        CosmosContainerDbConfig config = serviceProvider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig(identifier);
        ICosmosContainerAdapter container = serviceProvider.GetAzureComponentFactory().Cosmos.CreateContainer(config);
        await container.ValidateConnectionAsync(cancellationToken);
        return null;
    }
}

internal sealed class SqlDatabaseIsLiveTrigger(SqlDatabaseIdentifier identifier) : AzureIsLiveTriggerBase
{
    public override string Name => "SqlDatabase IsLive Trigger";

    public override string Description => "Checks whether the configured SQL database is reachable with the configured credentials.";

    public override Step<object?> Clone() => new SqlDatabaseIsLiveTrigger(identifier).WithClonedOptions(this);

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
}