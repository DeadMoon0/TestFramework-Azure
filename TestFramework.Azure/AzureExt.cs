using Azure.Data.Tables;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using System;
using System.Linq;
using TestFramework.Azure.Builder.FunctionAppBuilder;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Azure.Builder.LogicAppBuilder;
using TestFramework.Azure.DB.CosmosDB;
using TestFramework.Azure.DB.SqlServer;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Azure.FunctionApp.Trigger;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.LogicApp;
using TestFramework.Azure.LogicApp.Events;
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
public static class AzureExt
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
        /// Access Logic App trigger builders.
        /// </summary>
        public LogicAppTrigger LogicApp { get; } = new LogicAppTrigger();

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
    /// Creates Logic App triggers for remote Standard workflow execution.
    /// </summary>
    public class LogicAppTrigger
    {
        /// <summary>
        /// Starts a remote HTTP Logic App trigger builder for a configured Logic App identifier.
        /// </summary>
        /// <param name="identifier">The Logic App identifier to resolve.</param>
        /// <returns>The builder used to select a workflow and trigger.</returns>
        public ILogicAppWorkflowStage Http(LogicAppIdentifier identifier)
        {
            return new RemoteLogicAppBuilder(identifier);
        }
    }

    /// <summary>
    /// Creates Function App triggers for remote and managed execution. In-process execution comes with the
    /// <c>TestFramework.Azure.Functions</c> package, as extensions on this type.
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
        /// Calls a function through the host's admin endpoint by its function name, and returns its managed result.
        /// </summary>
        /// <param name="identifier">The Function App identifier to resolve.</param>
        /// <param name="functionName">The function's name as the host knows it: the name in its <c>[Function]</c> attribute.</param>
        /// <returns>A step that invokes the function and captures its result.</returns>
        /// <remarks>
        /// Needs no access to the function's code. To take the name from the function method's attributes instead, use
        /// <c>Managed&lt;T&gt;(identifier, methodName)</c> from <c>TestFramework.Azure.Functions</c>.
        /// </remarks>
        public Step<ManagedResult> ManagedFunction(FunctionAppIdentifier identifier, string functionName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
            return new ManagedRemoteFunctionAppTrigger(identifier, Var.Const(new TriggerRouting(functionName.Trim())));
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
            => CosmosRef<TItem>(identifier, Var.Const(id), Var.Const(partitionKey));

        /// <summary>
        /// Creates a reference to a Cosmos DB item named by variables, resolved when the reference is pinned.
        /// </summary>
        /// <typeparam name="TItem">The item type stored in Cosmos.</typeparam>
        /// <param name="identifier">The Cosmos container identifier.</param>
        /// <param name="id">The document id.</param>
        /// <param name="partitionKey">The document partition key.</param>
        /// <returns>A Cosmos artifact reference.</returns>
        public CosmosDbItemArtifactReference<TItem> CosmosRef<TItem>(CosmosContainerIdentifier identifier, VariableReference<string> id, VariableReference<PartitionKey> partitionKey)
            => new(identifier, partitionKey, id);

        /// <summary>
        /// Creates a reference to a SQL row addressed by its primary key values.
        /// </summary>
        /// <typeparam name="TRow">The entity type.</typeparam>
        /// <param name="identifier">The SQL database identifier.</param>
        /// <param name="primaryKeyValues">Primary key values in key-order.</param>
        /// <returns>A SQL row artifact reference.</returns>
        public AzureSqlRowArtifactReference<TRow> SqlRef<TRow>(SqlDatabaseIdentifier identifier, params VariableReference<string>[] primaryKeyValues) where TRow : class
        {
            return new AzureSqlRowArtifactReference<TRow>(identifier, primaryKeyValues);
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
        /// Access Logic App event factories.
        /// </summary>
        public LogicAppEvents LogicApp { get; } = new LogicAppEvents();

        /// <summary>
        /// Access Service Bus event factories.
        /// </summary>
        public ServiceBusEvents ServiceBus { get; } = new ServiceBusEvents();
    }

    /// <summary>
    /// Creates Logic App workflow run events.
    /// </summary>
    public class LogicAppEvents
    {
        /// <summary>
        /// Waits until the Logic App run represented by a normalized execution result reaches a specific status.
        /// </summary>
        public LogicAppRunEvent RunReachedStatus(LogicAppIdentifier identifier, VariableReference<LogicAppExecutionResult> executionResult, LogicAppRunStatus status)
            => new(identifier,
                executionResult.Transform(result => result?.RunContext.WorkflowName ?? string.Empty),
                executionResult.Transform(result => result?.RunContext.RunId ?? string.Empty),
                Var.Const(status),
                executionResult,
                typeof(LogicAppExecutionResult));

        /// <summary>
        /// Waits until the Logic App run represented by a normalized execution result reaches a specific status.
        /// </summary>
        public LogicAppRunEvent RunReachedStatus(LogicAppIdentifier identifier, VariableReference<LogicAppExecutionResult> executionResult, VariableReference<LogicAppRunStatus> status)
            => new(identifier,
                executionResult.Transform(result => result?.RunContext.WorkflowName ?? string.Empty),
                executionResult.Transform(result => result?.RunContext.RunId ?? string.Empty),
                status,
                executionResult,
                typeof(LogicAppExecutionResult));

        /// <summary>
        /// Waits until the Logic App run represented by an explicit run context reaches a specific status.
        /// </summary>
        public LogicAppRunEvent RunReachedStatus(LogicAppIdentifier identifier, VariableReference<LogicAppRunContext> runContext, LogicAppRunStatus status)
            => new(identifier,
                runContext.Transform(context => context?.WorkflowName ?? string.Empty),
                runContext.Transform(context => context?.RunId ?? string.Empty),
                Var.Const(status),
                runContext,
                typeof(LogicAppRunContext));

        /// <summary>
        /// Waits until the Logic App run represented by an explicit run context reaches a specific status.
        /// </summary>
        public LogicAppRunEvent RunReachedStatus(LogicAppIdentifier identifier, VariableReference<LogicAppRunContext> runContext, VariableReference<LogicAppRunStatus> status)
            => new(identifier,
                runContext.Transform(context => context?.WorkflowName ?? string.Empty),
                runContext.Transform(context => context?.RunId ?? string.Empty),
                status,
                runContext,
                typeof(LogicAppRunContext));

        /// <summary>
        /// Waits until the Logic App run represented by a trigger result reaches a specific status.
        /// </summary>
        public LogicAppRunEvent RunReachedStatus(LogicAppIdentifier identifier, VariableReference<LogicAppTriggerResult> triggerResult, LogicAppRunStatus status)
            => new(identifier,
                triggerResult.Transform(result => result?.WorkflowName ?? string.Empty),
                triggerResult.Transform(result => result?.RunId ?? string.Empty),
                Var.Const(status),
                triggerResult,
                typeof(LogicAppTriggerResult));

        /// <summary>
        /// Waits until the Logic App run represented by a trigger result reaches a specific status.
        /// </summary>
        public LogicAppRunEvent RunReachedStatus(LogicAppIdentifier identifier, VariableReference<LogicAppTriggerResult> triggerResult, VariableReference<LogicAppRunStatus> status)
            => new(identifier,
                triggerResult.Transform(result => result?.WorkflowName ?? string.Empty),
                triggerResult.Transform(result => result?.RunId ?? string.Empty),
                status,
                triggerResult,
                typeof(LogicAppTriggerResult));

        /// <summary>
        /// Waits until a Logic App run reaches a specific status.
        /// </summary>
        /// <remarks>Uses <c>null</c> for <c>workflowName</c>; the other overload states it.</remarks>
        public LogicAppRunEvent RunReachedStatus(LogicAppIdentifier identifier, VariableReference<string> runId, LogicAppRunStatus status)
            => this.RunReachedStatus(identifier, runId, status, null);

        /// <summary>
        /// Waits until a Logic App run reaches a specific status.
        /// </summary>
        public LogicAppRunEvent RunReachedStatus(LogicAppIdentifier identifier, VariableReference<string> runId, LogicAppRunStatus status, VariableReference<string>? workflowName)
            => new(identifier, workflowName, runId, Var.Const(status));

        /// <summary>
        /// Waits until a Logic App run reaches a specific status.
        /// </summary>
        /// <remarks>Uses <c>null</c> for <c>workflowName</c>; the other overload states it.</remarks>
        public LogicAppRunEvent RunReachedStatus(LogicAppIdentifier identifier, VariableReference<string> runId, VariableReference<LogicAppRunStatus> status)
            => this.RunReachedStatus(identifier, runId, status, null);

        /// <summary>
        /// Waits until a Logic App run reaches a specific status.
        /// </summary>
        public LogicAppRunEvent RunReachedStatus(LogicAppIdentifier identifier, VariableReference<string> runId, VariableReference<LogicAppRunStatus> status, VariableReference<string>? workflowName)
            => new(identifier, workflowName, runId, status);

        /// <summary>
        /// Waits until a Logic App run represented by a normalized execution result reaches any terminal state.
        /// </summary>
        public LogicAppRunEvent RunCompleted(LogicAppIdentifier identifier, VariableReference<LogicAppExecutionResult> executionResult)
            => new(identifier,
                executionResult.Transform(result => result?.RunContext.WorkflowName ?? string.Empty),
                executionResult.Transform(result => result?.RunContext.RunId ?? string.Empty),
                null,
                executionResult,
                typeof(LogicAppExecutionResult));

        /// <summary>
        /// Waits until a Logic App run represented by an explicit run context reaches any terminal state.
        /// </summary>
        public LogicAppRunEvent RunCompleted(LogicAppIdentifier identifier, VariableReference<LogicAppRunContext> runContext)
            => new(identifier,
                runContext.Transform(context => context?.WorkflowName ?? string.Empty),
                runContext.Transform(context => context?.RunId ?? string.Empty),
                null,
                runContext,
                typeof(LogicAppRunContext));

        /// <summary>
        /// Waits until a Logic App run reaches any terminal state.
        /// </summary>
        public LogicAppRunEvent RunCompleted(LogicAppIdentifier identifier, VariableReference<LogicAppTriggerResult> triggerResult)
            => new(identifier,
                triggerResult.Transform(result => result?.WorkflowName ?? string.Empty),
                triggerResult.Transform(result => result?.RunId ?? string.Empty),
                null,
                triggerResult,
                typeof(LogicAppTriggerResult));

        /// <summary>
        /// Waits until a Logic App run reaches any terminal state.
        /// </summary>
        /// <remarks>Uses <c>null</c> for <c>workflowName</c>; the other overload states it.</remarks>
        public LogicAppRunEvent RunCompleted(LogicAppIdentifier identifier, VariableReference<string> runId)
            => this.RunCompleted(identifier, runId, null);

        /// <summary>
        /// Waits until a Logic App run reaches any terminal state.
        /// </summary>
        public LogicAppRunEvent RunCompleted(LogicAppIdentifier identifier, VariableReference<string> runId, VariableReference<string>? workflowName)
            => new(identifier, workflowName, runId, null);
    }

    /// <summary>
    /// Creates Service Bus events.
    /// </summary>
    public class ServiceBusEvents
    {
        /// <summary>
        /// Creates a Service Bus message-received event that matches any message on the entity.
        /// </summary>
        /// <remarks>
        /// Narrow it with <c>.WithMessageId(...)</c>, <c>.WithCorrelationId(...)</c> and <c>.Where(...)</c>; a topic entry
        /// without a subscription of its own takes <c>.WithTempSubscription()</c>. The matching message is completed
        /// unless <c>.CompleteMessage(false)</c> says otherwise.
        /// </remarks>
        /// <param name="identifier">The Service Bus identifier.</param>
        /// <returns>A Service Bus event that completes when a matching message is observed.</returns>
        public ServiceBusProcessEvent MessageReceived(ServiceBusIdentifier identifier)
        {
            ArgumentNullException.ThrowIfNull(identifier);
            return new ServiceBusProcessEvent(identifier, null, null, null, null, null);
        }
    }
}