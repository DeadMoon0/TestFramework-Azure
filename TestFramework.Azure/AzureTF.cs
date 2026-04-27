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

/// <summary>
/// Entry point for the Azure-specific TestFramework DSL.
/// </summary>
public static class AzureTF
{
    /// <summary>
    /// Access Azure triggers and liveness checks.
    /// </summary>
    public static TriggerProxy Trigger { get; } = new TriggerProxy();

    /// <summary>
    /// Access Azure artifact reference factories.
    /// </summary>
    public static ArtifactProxy Artifact { get; } = new ArtifactProxy();

    /// <summary>
    /// Access Azure artifact query finders.
    /// </summary>
    public static ArtifactFinderProxy ArtifactFinder { get; } = new ArtifactFinderProxy();

    /// <summary>
    /// Access Azure event factories.
    /// </summary>
    public static EventProxy Event { get; } = new EventProxy();

    /// <summary>
    /// Groups Azure trigger factories.
    /// </summary>
    public class TriggerProxy
    {
        /// <summary>
        /// Access Function App trigger builders.
        /// </summary>
        public FunctionAppTrigger FunctionApp { get; } = new FunctionAppTrigger();

        /// <summary>
        /// Access Service Bus trigger builders.
        /// </summary>
        public ServiceBusTrigger ServiceBus { get; } = new ServiceBusTrigger();

        /// <summary>
        /// Access Azure liveness triggers.
        /// </summary>
        public IsLiveTrigger IsLive { get; } = new IsLiveTrigger();
    }

    /// <summary>
    /// Creates Function App triggers for remote, managed, and in-process execution.
    /// </summary>
    public class FunctionAppTrigger
    {
        /// <summary>
        /// Starts a remote HTTP Function App trigger builder for a configured Function App identifier.
        /// </summary>
        /// <param name="identifier">The Function App identifier to resolve.</param>
        /// <returns>The next builder stage for selecting an endpoint.</returns>
        public IFunctionAppHttpConnectionStage Http(FunctionAppIdentifier identifier)
        {
            return new RemoteFunctionAppBuilder(identifier);
        }

        /// <summary>
        /// Calls a managed function by method name and returns its managed result.
        /// </summary>
        /// <typeparam name="TFunction">The function host type containing the target method.</typeparam>
        /// <param name="identifier">The Function App identifier to resolve.</param>
        /// <param name="methodName">The method marked with the Azure Functions trigger attributes.</param>
        /// <returns>A step that invokes the function and captures its result.</returns>
        public Step<ManagedResult> Managed<TFunction>(FunctionAppIdentifier identifier, string methodName) where TFunction : notnull
        {
            return new ManagedFunctionAppBuilder(identifier)
                .SelectFunctionWithMethod<TFunction>(methodName)
                .Call();
        }

        /// <summary>
        /// Builds an in-process HTTP Function App trigger from a synchronous delegate.
        /// </summary>
        /// <typeparam name="TFunction">The function type to instantiate from DI.</typeparam>
        /// <param name="action">The delegate that executes the in-process function logic.</param>
        /// <returns>The payload stage for building the request.</returns>
        public IFunctionAppHttpPayloadStage InProcessHttp<TFunction>(Action<TFunction, FunctionAppHttpInProcessCallProxy> action) where TFunction : notnull
        {
            return new InProcessHttpFunctionAppBuilder<TFunction>(action);
        }

        /// <summary>
        /// Builds an in-process HTTP Function App trigger from an asynchronous delegate.
        /// </summary>
        /// <typeparam name="TFunction">The function type to instantiate from DI.</typeparam>
        /// <param name="action">The asynchronous delegate that executes the in-process function logic.</param>
        /// <returns>The payload stage for building the request.</returns>
        public IFunctionAppHttpPayloadStage InProcessHttp<TFunction>(Func<TFunction, FunctionAppHttpInProcessCallProxy, Task> action) where TFunction : notnull
        {
            return new InProcessHttpFunctionAppBuilder<TFunction>(action);
        }

        /// <summary>
        /// Builds an in-process HTTP Function App trigger from a synchronous delegate that returns an action result.
        /// </summary>
        /// <typeparam name="TFunction">The function type to instantiate from DI.</typeparam>
        /// <param name="func">The delegate that executes the in-process function logic.</param>
        /// <returns>The payload stage for building the request.</returns>
        public IFunctionAppHttpPayloadStage InProcessHttp<TFunction>(Func<TFunction, FunctionAppHttpInProcessCallProxy, IActionResult> func) where TFunction : notnull
        {
            return new InProcessHttpFunctionAppBuilder<TFunction>(func);
        }

        /// <summary>
        /// Builds an in-process HTTP Function App trigger from an asynchronous delegate that returns an action result.
        /// </summary>
        /// <typeparam name="TFunction">The function type to instantiate from DI.</typeparam>
        /// <param name="func">The asynchronous delegate that executes the in-process function logic.</param>
        /// <returns>The payload stage for building the request.</returns>
        public IFunctionAppHttpPayloadStage InProcessHttp<TFunction>(Func<TFunction, FunctionAppHttpInProcessCallProxy, Task<IActionResult>> func) where TFunction : notnull
        {
            return new InProcessHttpFunctionAppBuilder<TFunction>(func);
        }
    }

    /// <summary>
    /// Creates Service Bus triggers.
    /// </summary>
    public class ServiceBusTrigger
    {
        /// <summary>
        /// Creates a trigger that sends a Service Bus message.
        /// </summary>
        /// <param name="identifier">The Service Bus identifier to resolve.</param>
        /// <param name="message">The message variable to send.</param>
        /// <returns>A step that sends the message when executed.</returns>
        public ServiceBusSendTrigger Send(ServiceBusIdentifier identifier, VariableReference<ServiceBusMessage> message)
        {
            return new ServiceBusSendTrigger(identifier, message);
        }
    }

    /// <summary>
    /// Groups Azure artifact reference factories.
    /// </summary>
    public class ArtifactProxy
    {
        /// <summary>
        /// Access database artifact reference factories.
        /// </summary>
        public DBArtifacts DB { get; } = new DBArtifacts();

        /// <summary>
        /// Access Storage Account artifact reference factories.
        /// </summary>
        public StorageAccountArtifacts StorageAccount { get; } = new StorageAccountArtifacts();
    }

    /// <summary>
    /// Creates database artifact references.
    /// </summary>
    public class DBArtifacts
    {
        /// <summary>
        /// Creates a reference to a Cosmos DB item.
        /// </summary>
        /// <typeparam name="TItem">The item type stored in Cosmos.</typeparam>
        /// <param name="identifier">The Cosmos container identifier.</param>
        /// <param name="id">The document id.</param>
        /// <param name="partitionKey">The document partition key.</param>
        /// <returns>A Cosmos artifact reference.</returns>
        public CosmosDbItemArtifactReference<TItem> CosmosRef<TItem>(CosmosContainerIdentifier identifier, string id, PartitionKey partitionKey)
        {
            return new CosmosDbItemArtifactReference<TItem>(identifier, partitionKey, id);
        }

        /// <summary>
        /// Creates a reference to a SQL row addressed by its primary key values.
        /// </summary>
        /// <typeparam name="TRow">The entity type.</typeparam>
        /// <param name="identifier">The SQL database identifier.</param>
        /// <param name="primaryKeyValues">Primary key values in key-order.</param>
        /// <returns>A SQL row artifact reference.</returns>
        public SqlRowArtifactReference<TRow> SqlRef<TRow>(SqlDatabaseIdentifier identifier, params VariableReference<string>[] primaryKeyValues) where TRow : class
        {
            return new SqlRowArtifactReference<TRow>(identifier, primaryKeyValues);
        }
    }

    /// <summary>
    /// Creates Storage Account artifact references.
    /// </summary>
    public class StorageAccountArtifacts
    {
        /// <summary>
        /// Creates a reference to an Azure Table entity.
        /// </summary>
        /// <typeparam name="T">The table entity type.</typeparam>
        /// <param name="identifier">The Storage Account identifier.</param>
        /// <param name="tableName">The table name variable.</param>
        /// <param name="partitionKey">The partition key variable.</param>
        /// <param name="rowKey">The row key variable.</param>
        /// <returns>A table entity artifact reference.</returns>
        public TableStorageEntityArtifactReference<T> TableRef<T>(StorageAccountIdentifier identifier, VariableReference<string> tableName, VariableReference<string> partitionKey, VariableReference<string> rowKey) where T : class, ITableEntity
        {
            return new TableStorageEntityArtifactReference<T>(identifier, tableName, partitionKey, rowKey);
        }

        /// <summary>
        /// Creates a reference to a blob inside the configured blob container.
        /// </summary>
        /// <param name="identifier">The Storage Account identifier.</param>
        /// <param name="path">The blob path variable.</param>
        /// <returns>A blob artifact reference.</returns>
        public StorageAccountBlobArtifactReference BlobRef(StorageAccountIdentifier identifier, VariableReference<string> path)
        {
            return new StorageAccountBlobArtifactReference(identifier, path);
        }
    }

    /// <summary>
    /// Groups Azure artifact finder factories.
    /// </summary>
    public class ArtifactFinderProxy
    {
        /// <summary>
        /// Access database artifact query finders.
        /// </summary>
        public DBArtifactFinder DB { get; } = new DBArtifactFinder();

        /// <summary>
        /// Access Storage Account artifact query finders.
        /// </summary>
        public StorageAccountArtifactFinder StorageAccount { get; } = new StorageAccountArtifactFinder();
    }

    /// <summary>
    /// Creates database artifact query finders.
    /// </summary>
    public class DBArtifactFinder
    {
        /// <summary>
        /// Creates a Cosmos DB query finder that returns references for matching items.
        /// </summary>
        /// <typeparam name="TItem">The item type returned by the query.</typeparam>
        /// <param name="dbIdentifier">The Cosmos container identifier.</param>
        /// <param name="query">The query definition variable.</param>
        /// <returns>A Cosmos query finder.</returns>
        public CosmosDbItemArtifactQueryFinder<TItem> CosmosQuery<TItem>(CosmosContainerIdentifier dbIdentifier, VariableReference<QueryDefinition> query)
        {
            return new CosmosDbItemArtifactQueryFinder<TItem>(dbIdentifier, query);
        }

        /// <summary>
        /// Creates an EF Core query finder that returns references for matching SQL rows.
        /// </summary>
        /// <typeparam name="TRow">The entity type returned by the query.</typeparam>
        /// <param name="dbIdentifier">The SQL database identifier.</param>
        /// <param name="queryModifier">A delegate that shapes the EF Core query.</param>
        /// <returns>A SQL query finder.</returns>
        public SqlEFCoreArtifactQueryFinder<TRow> SqlQuery<TRow>(SqlDatabaseIdentifier dbIdentifier, Func<IQueryable<TRow>, IQueryable<TRow>> queryModifier) where TRow : class
        {
            return new SqlEFCoreArtifactQueryFinder<TRow>(dbIdentifier, queryModifier);
        }
    }

    /// <summary>
    /// Creates Storage Account artifact query finders.
    /// </summary>
    public class StorageAccountArtifactFinder
    {
        /// <summary>
        /// Creates an Azure Table query finder that returns references for matching entities.
        /// </summary>
        /// <typeparam name="T">The table entity type.</typeparam>
        /// <param name="identifier">The Storage Account identifier.</param>
        /// <param name="tableName">The table name variable.</param>
        /// <param name="filter">The Azure Table filter expression.</param>
        /// <returns>A table query finder.</returns>
        public TableStorageEntityArtifactQueryFinder<T> TableQuery<T>(StorageAccountIdentifier identifier, VariableReference<string> tableName, string filter) where T : class, ITableEntity
        {
            return new TableStorageEntityArtifactQueryFinder<T>(identifier, tableName, filter);
        }
    }

    /// <summary>
    /// Groups Azure event factories.
    /// </summary>
    public class EventProxy
    {
        /// <summary>
        /// Access Service Bus event factories.
        /// </summary>
        public ServiceBusEvents ServiceBus { get; } = new ServiceBusEvents();
    }

    /// <summary>
    /// Creates Service Bus events.
    /// </summary>
    public class ServiceBusEvents
    {
        /// <summary>
        /// Creates a Service Bus message-received event using a plain boolean flag for temporary subscription creation.
        /// </summary>
        /// <param name="identifier">The Service Bus identifier.</param>
        /// <param name="messageId">Optional message id filter.</param>
        /// <param name="correlationId">Optional correlation id filter.</param>
        /// <param name="predicate">Optional custom predicate applied to received messages.</param>
        /// <param name="completeMessage">Whether matching messages should be completed automatically.</param>
        /// <param name="createTempSubscription">Whether a temporary topic subscription should be created for the receive flow.</param>
        /// <returns>A Service Bus event that completes when a matching message is observed.</returns>
        public ServiceBusProcessEvent MessageReceived(ServiceBusIdentifier identifier, VariableReference<string>? messageId = null, VariableReference<string>? correlationId = null, VariableReference<Func<ServiceBusReceivedMessage, bool>>? predicate = null, VariableReference<bool>? completeMessage = null, bool createTempSubscription = false)
            => new ServiceBusProcessEvent(identifier, messageId, correlationId, predicate, completeMessage,
                createTempSubscription ? Var.Const(createTempSubscription) : null);

        /// <summary>
        /// Creates a Service Bus message-received event using a variable-backed temporary subscription flag.
        /// </summary>
        /// <typeparam name="TVar">The variable type used for the temporary-subscription flag.</typeparam>
        /// <param name="identifier">The Service Bus identifier.</param>
        /// <param name="messageId">Optional message id filter.</param>
        /// <param name="correlationId">Optional correlation id filter.</param>
        /// <param name="predicate">Optional custom predicate applied to received messages.</param>
        /// <param name="completeMessage">Whether matching messages should be completed automatically.</param>
        /// <param name="createTempSubscription">Variable-backed flag controlling temporary topic subscription creation.</param>
        /// <returns>A Service Bus event that completes when a matching message is observed.</returns>
        public ServiceBusProcessEvent MessageReceived<TVar>(ServiceBusIdentifier identifier, VariableReference<string>? messageId = null, VariableReference<string>? correlationId = null, VariableReference<Func<ServiceBusReceivedMessage, bool>>? predicate = null, VariableReference<bool>? completeMessage = null, ImmutableVariable<TVar, bool>? createTempSubscription = null) where TVar : VariableReference<bool>
            => new ServiceBusProcessEvent(identifier, messageId, correlationId, predicate, completeMessage, createTempSubscription);
    }
}