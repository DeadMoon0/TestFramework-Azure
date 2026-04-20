using Azure.Data.Tables;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Azure.Builder.FunctionAppBuilder;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Azure.DB.CosmosDB;
using TestFramework.Azure.DB.SqlServer;
using TestFramework.Azure.FunctionApp.InProcessProxies;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.ServiceBus;
using TestFramework.Azure.StorageAccount.Blob;
using TestFramework.Azure.Trigger.IsLive;
using TestFramework.Azure.StorageAccount.Table;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure;

public static class AzureTF
{
    public static TriggerProxy Trigger { get; } = new TriggerProxy();
    public static ArtifactProxy Artifact { get; } = new ArtifactProxy();
    public static ArtifactFinderProxy ArtifactFinder { get; } = new ArtifactFinderProxy();
    public static EventProxy Event { get; } = new EventProxy();

    public class TriggerProxy
    {
        public FunctionAppTrigger FunctionApp { get; } = new FunctionAppTrigger();
        public ServiceBusTrigger ServiceBus { get; } = new ServiceBusTrigger();
        public IsLiveTrigger IsLive { get; } = new IsLiveTrigger();
    }

    public class FunctionAppTrigger
    {
        public IFunctionAppHttpConnectionStage Http(FunctionAppIdentifier identifier)
        {
            return new RemoteFunctionAppBuilder(identifier);
        }

        public Step<ManagedResult> Managed<TFunction>(FunctionAppIdentifier identifier, string methodName) where TFunction : notnull
        {
            return new ManagedFunctionAppBuilder(identifier)
                .SelectFunctionWithMethod<TFunction>(methodName)
                .Call();
        }

        public IFunctionAppHttpPayloadStage InProcessHttp<TFunction>(Action<TFunction, FunctionAppHttpInProcessCallProxy> action) where TFunction : notnull
        {
            return new InProcessHttpFunctionAppBuilder<TFunction>(action);
        }
        public IFunctionAppHttpPayloadStage InProcessHttp<TFunction>(Func<TFunction, FunctionAppHttpInProcessCallProxy, Task> action) where TFunction : notnull
        {
            return new InProcessHttpFunctionAppBuilder<TFunction>(action);
        }
        public IFunctionAppHttpPayloadStage InProcessHttp<TFunction>(Func<TFunction, FunctionAppHttpInProcessCallProxy, IActionResult> func) where TFunction : notnull
        {
            return new InProcessHttpFunctionAppBuilder<TFunction>(func);
        }
        public IFunctionAppHttpPayloadStage InProcessHttp<TFunction>(Func<TFunction, FunctionAppHttpInProcessCallProxy, Task<IActionResult>> func) where TFunction : notnull
        {
            return new InProcessHttpFunctionAppBuilder<TFunction>(func);
        }
    }

    public class ServiceBusTrigger
    {
        public ServiceBusSendTrigger Send(ServiceBusIdentifier identifier, VariableReference<ServiceBusMessage> message)
        {
            return new ServiceBusSendTrigger(identifier, message);
        }
    }

    public class ArtifactProxy
    {
        public DBArtifacts DB { get; } = new DBArtifacts();
        public StorageAccountArtifacts StorageAccount { get; } = new StorageAccountArtifacts();
    }

    public class DBArtifacts
    {
        public CosmosDbItemArtifactReference<TItem> CosmosRef<TItem>(CosmosContainerIdentifier identifier, string id, PartitionKey partitionKey)
        {
            return new CosmosDbItemArtifactReference<TItem>(identifier, partitionKey, id);
        }

        public SqlRowArtifactReference<TRow> SqlRef<TRow>(SqlDatabaseIdentifier identifier, params VariableReference<string>[] primaryKeyValues) where TRow : class
        {
            return new SqlRowArtifactReference<TRow>(identifier, primaryKeyValues);
        }
    }

    public class StorageAccountArtifacts
    {
        public TableStorageEntityArtifactReference<T> TableRef<T>(StorageAccountIdentifier identifier, VariableReference<string> tableName, VariableReference<string> partitionKey, VariableReference<string> rowKey) where T : class, ITableEntity
        {
            return new TableStorageEntityArtifactReference<T>(identifier, tableName, partitionKey, rowKey);
        }

        public StorageAccountBlobArtifactReference BlobRef(StorageAccountIdentifier identifier, VariableReference<string> path)
        {
            return new StorageAccountBlobArtifactReference(identifier, path);
        }
    }

    public class ArtifactFinderProxy
    {
        public DBArtifactFinder DB { get; } = new DBArtifactFinder();
        public StorageAccountArtifactFinder StorageAccount { get; } = new StorageAccountArtifactFinder();
    }

    public class DBArtifactFinder
    {
        public CosmosDbItemArtifactQueryFinder<TItem> CosmosQuery<TItem>(CosmosContainerIdentifier dbIdentifier, VariableReference<QueryDefinition> query)
        {
            return new CosmosDbItemArtifactQueryFinder<TItem>(dbIdentifier, query);
        }

        public SqlEFCoreArtifactQueryFinder<TRow> SqlQuery<TRow>(SqlDatabaseIdentifier dbIdentifier, Func<IQueryable<TRow>, IQueryable<TRow>> queryModifier) where TRow : class
        {
            return new SqlEFCoreArtifactQueryFinder<TRow>(dbIdentifier, queryModifier);
        }
    }

    public class StorageAccountArtifactFinder
    {
        public TableStorageEntityArtifactQueryFinder<T> TableQuery<T>(StorageAccountIdentifier identifier, VariableReference<string> tableName, string filter) where T : class, ITableEntity
        {
            return new TableStorageEntityArtifactQueryFinder<T>(identifier, tableName, filter);
        }
    }

    public class EventProxy
    {
        public ServiceBusEvents ServiceBus { get; } = new ServiceBusEvents();
    }

    public class ServiceBusEvents
    {
        // Convenience overload — plain bool, mirrors the Conditional(bool) pattern.
        public ServiceBusProcessEvent MessageReceived(ServiceBusIdentifier identifier, VariableReference<string>? messageId = null, VariableReference<string>? correlationId = null, VariableReference<Func<ServiceBusReceivedMessage, bool>>? predicate = null, VariableReference<bool>? completeMessage = null, bool createTempSubscription = false)
            => new ServiceBusProcessEvent(identifier, messageId, correlationId, predicate, completeMessage,
                createTempSubscription ? Var.Const(createTempSubscription) : null);

        // Primary overload — ImmutableVariable<TVar, bool> mirrors the Conditional<TVar> pattern.
        public ServiceBusProcessEvent MessageReceived<TVar>(ServiceBusIdentifier identifier, VariableReference<string>? messageId = null, VariableReference<string>? correlationId = null, VariableReference<Func<ServiceBusReceivedMessage, bool>>? predicate = null, VariableReference<bool>? completeMessage = null, ImmutableVariable<TVar, bool>? createTempSubscription = null) where TVar : VariableReference<bool>
            => new ServiceBusProcessEvent(identifier, messageId, correlationId, predicate, completeMessage, createTempSubscription);
    }
}