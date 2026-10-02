using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Data.Tables;
using Azure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.DB.CosmosDB;

using TestFramework.Azure.ServiceBus;

namespace TestFramework.Azure.Runtime;

internal interface IAzureComponentFactory
{
    ICosmosComponentFactory Cosmos { get; }
    IBlobComponentFactory Blob { get; }
    ITableComponentFactory Table { get; }
    IServiceBusComponentFactory ServiceBus { get; }
    IHttpComponentFactory Http { get; }
}

internal interface IBlobComponentFactory
{
    IBlobContainerAdapter CreateContainer(Resolved<StorageAccountConfig> account);
}

internal interface IBlobContainerAdapter
{
    // Every operation takes the caller's token: the callers all hold context.Deadline.Token, and an
    // adapter without the parameter was the one thing keeping it from the SDK - a hung endpoint then
    // retried on the SDK's schedule while the step's deadline had long fired.
    Task ValidateServiceConnectionAsync(CancellationToken cancellationToken);
    Task ValidateConnectionAsync(CancellationToken cancellationToken);
    Task CreateIfNotExistsAsync(CancellationToken cancellationToken);
    Task DeleteBlobAsync(string path, CancellationToken cancellationToken);
    Task UploadBlobAsync(string path, byte[] data, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken);
    Task<BlobReadResponse> ReadBlobAsync(string path, CancellationToken cancellationToken);
}

internal readonly record struct BlobReadResponse(bool Found, byte[]? Data, IReadOnlyDictionary<string, string>? Metadata);

internal interface ITableComponentFactory
{
    ITableAdapter CreateTable(Resolved<StorageAccountConfig> account, string tableName);
}

internal interface ITableAdapter
{
    Task ValidateServiceConnectionAsync(CancellationToken cancellationToken);
    Task ValidateConnectionAsync(CancellationToken cancellationToken);
    Task CreateIfNotExistsAsync(CancellationToken cancellationToken);
    Task UpsertEntityAsync<T>(T entity, CancellationToken cancellationToken) where T : class, ITableEntity;
    Task DeleteEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken);
    Task<TableReadResponse<T>> GetEntityAsync<T>(string partitionKey, string rowKey, CancellationToken cancellationToken) where T : class, ITableEntity;
    IAsyncEnumerable<T> QueryEntitiesAsync<T>(string filter, CancellationToken cancellationToken) where T : class, ITableEntity;
}

internal readonly record struct TableReadResponse<T>(bool Found, T? Entity) where T : class, ITableEntity;

internal interface ICosmosComponentFactory
{
    ICosmosContainerAdapter CreateContainer(Resolved<CosmosContainerDbConfig> container);
}

internal interface ICosmosContainerAdapter
{
    Task ValidateAccountReachabilityAsync(CancellationToken cancellationToken);
    Task ValidateAccountConnectionAsync(CancellationToken cancellationToken);
    Task ValidateConnectionAsync(CancellationToken cancellationToken);
    Task EnsureContainerExistsAsync<TItem>(TItem item, CancellationToken cancellationToken);
    Task DeleteItemAsync<TItem>(string id, PartitionKey partitionKey, CancellationToken cancellationToken);
    Task UpsertItemAsync<TItem>(TItem item, PartitionKey partitionKey, CancellationToken cancellationToken);
    Task<CosmosReadResponse> ReadItemAsync(string id, PartitionKey partitionKey, CancellationToken cancellationToken);
    IAsyncEnumerable<TItem> QueryItemsAsync<TItem>(QueryDefinition query, CancellationToken cancellationToken);
}

internal readonly record struct CosmosReadResponse(bool Found, Stream? Content);

internal interface IServiceBusComponentFactory
{
    IServiceBusSenderAdapter CreateSender(Resolved<ServiceBusConfig> serviceBus);
    IServiceBusMessagePump CreateMessagePump(Resolved<ServiceBusConfig> serviceBus, string? subscriptionName);
    IServiceBusAdministrationAdapter CreateAdministration(Resolved<ServiceBusConfig> serviceBus);
}

internal interface IServiceBusSenderAdapter : IAsyncDisposable
{
    Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken);
}

/// <param name="MessageId">Optional message id filter.</param>
/// <param name="CorrelationId">Optional correlation id filter.</param>
/// <param name="Predicate">Optional custom filter.</param>
/// <param name="CompleteMessage">Whether a matching message is completed.</param>
/// <param name="AbandonNonMatching">
/// Whether non-matching messages are abandoned so their lock is released immediately. False for a
/// temporary subscription, which filters server-side and therefore never sees a non-matching message.
/// </param>
/// <param name="Warn">Receives a one-off warning the first time a message is abandoned, and each transient error the processor retries.</param>
internal readonly record struct ServiceBusReceiveRequest(
    string? MessageId,
    string? CorrelationId,
    Func<ServiceBusReceivedMessage, bool>? Predicate,
    bool CompleteMessage,
    bool AbandonNonMatching = false,
    Action<string>? Warn = null);

internal interface IServiceBusMessagePump : IAsyncDisposable
{
    Task<ServiceBusReceivedMessage> ReceiveMessageAsync(ServiceBusReceiveRequest request, CancellationToken cancellationToken);
}

internal interface IServiceBusAdministrationAdapter
{
    Task ValidateNamespaceConnectionAsync(CancellationToken cancellationToken);
    Task ValidateConnectionAsync(CancellationToken cancellationToken);
    Task CreateSubscriptionAsync(CreateSubscriptionOptions options, CreateRuleOptions? ruleOptions, CancellationToken cancellationToken);
    Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken);
}

internal interface IHttpComponentFactory
{
    IHttpRequestSender CreateSender();
}

internal interface IHttpRequestSender
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}

internal static class AzureComponentFactoryServiceProviderExtensions
{
    private static readonly ConditionalWeakTable<IServiceProvider, IAzureComponentFactory> DefaultFactories = new();

    internal static IAzureComponentFactory GetAzureComponentFactory(this IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<IAzureComponentFactory>()
            ?? DefaultFactories.GetValue(serviceProvider, provider => new DefaultAzureComponentFactory(provider));
    }
}

internal sealed class DefaultAzureComponentFactory : IAzureComponentFactory
{
    public ICosmosComponentFactory Cosmos { get; }
    public IBlobComponentFactory Blob { get; }
    public ITableComponentFactory Table { get; }
    public IServiceBusComponentFactory ServiceBus { get; }
    public IHttpComponentFactory Http { get; } = new DefaultHttpComponentFactory();

    private readonly IServiceProvider _serviceProvider;

    internal DefaultAzureComponentFactory(IServiceProvider serviceProvider)
        : this(serviceProvider, serviceProvider.GetService<AzureClientCache>() ?? new AzureClientCache())
    {
    }

    internal DefaultAzureComponentFactory(IServiceProvider serviceProvider, AzureClientCache clientCache)
    {
        _serviceProvider = serviceProvider;
        Cosmos = new DefaultCosmosComponentFactory(_serviceProvider, clientCache);
        Blob = new DefaultBlobComponentFactory(clientCache);
        Table = new DefaultTableComponentFactory(clientCache);
        ServiceBus = new DefaultServiceBusComponentFactory(clientCache);
    }

    private sealed class DefaultCosmosComponentFactory(IServiceProvider serviceProvider, AzureClientCache clientCache) : ICosmosComponentFactory
    {
        public ICosmosContainerAdapter CreateContainer(Resolved<CosmosContainerDbConfig> container)
        {
            CosmosContainerDbConfig config = container.Value;

            // Keyed by the whole config record: CosmosClient is a documented singleton, and the
            // options provider may return different options for different configurations.
            CosmosClient client = clientCache.GetOrAdd(config, () =>
            {
                CosmosClientOptions options = CosmosClientOptionsResolver.Resolve(serviceProvider, config);
                return new CosmosClient(config.ConnectionString, options);
            });

            return new DefaultCosmosContainerAdapter(client, config);
        }
    }

    private sealed class DefaultBlobComponentFactory(AzureClientCache clientCache) : IBlobComponentFactory
    {
        public IBlobContainerAdapter CreateContainer(Resolved<StorageAccountConfig> account)
        {
            StorageAccountConfig config = account.Value;
            BlobServiceClient client = clientCache.GetOrAdd(config.ConnectionString, () => new BlobServiceClient(config.ConnectionString));
            BlobContainerClient container = client.GetBlobContainerClient(config.BlobContainerNameRequired);
            return new DefaultBlobContainerAdapter(client, container);
        }
    }

    private sealed class DefaultBlobContainerAdapter(BlobServiceClient serviceClient, BlobContainerClient container) : IBlobContainerAdapter
    {
        public Task ValidateServiceConnectionAsync(CancellationToken cancellationToken)
        {
            return serviceClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        }

        public async Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            if (!await container.ExistsAsync(cancellationToken))
            {
                throw new TestFramework.Core.Exceptions.FrameworkStateException($"Blob container '{container.Name}' was not found or is not accessible.");
            }
        }

        public Task CreateIfNotExistsAsync(CancellationToken cancellationToken) => container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        public Task DeleteBlobAsync(string path, CancellationToken cancellationToken) => container.DeleteBlobAsync(path, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);

        public async Task UploadBlobAsync(string path, byte[] data, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
        {
            BlobClient blob = container.GetBlobClient(path);
            await blob.UploadAsync(new BinaryData(data), true, cancellationToken);
            await blob.SetMetadataAsync(new Dictionary<string, string>(metadata), cancellationToken: cancellationToken);
        }

        public async Task<BlobReadResponse> ReadBlobAsync(string path, CancellationToken cancellationToken)
        {
            BlobClient blob = container.GetBlobClient(path);
            if (!await blob.ExistsAsync(cancellationToken))
            {
                return new BlobReadResponse(false, null, null);
            }

            BlobDownloadResult content = (await blob.DownloadContentAsync(cancellationToken)).Value;
            BlobProperties properties = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
            return new BlobReadResponse(true, content.Content.ToArray(), new Dictionary<string, string>(properties.Metadata));
        }
    }

    private sealed class DefaultTableComponentFactory(AzureClientCache clientCache) : ITableComponentFactory
    {
        public ITableAdapter CreateTable(Resolved<StorageAccountConfig> account, string tableName)
        {
            StorageAccountConfig config = account.Value;
            TableServiceClient serviceClient = clientCache.GetOrAdd(config.ConnectionString, () => new TableServiceClient(config.ConnectionString));
            TableClient tableClient = serviceClient.GetTableClient(tableName);
            return new DefaultTableAdapter(serviceClient, tableClient);
        }
    }

    private sealed class DefaultTableAdapter(TableServiceClient serviceClient, TableClient tableClient) : ITableAdapter
    {
        public async Task ValidateServiceConnectionAsync(CancellationToken cancellationToken)
        {
            await foreach (var _ in serviceClient.QueryAsync(cancellationToken: cancellationToken)
                .AsPages(pageSizeHint: 1)
                .ConfigureAwait(false))
            {
                break;
            }
        }

        public async Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            await foreach (Page<TableEntity> _ in tableClient.QueryAsync<TableEntity>(maxPerPage: 1, cancellationToken: cancellationToken)
                .AsPages(pageSizeHint: 1))
            {
                break;
            }
        }

        public Task CreateIfNotExistsAsync(CancellationToken cancellationToken) => tableClient.CreateIfNotExistsAsync(cancellationToken);

        public Task UpsertEntityAsync<T>(T entity, CancellationToken cancellationToken) where T : class, ITableEntity => tableClient.UpsertEntityAsync(entity, cancellationToken: cancellationToken);

        public Task DeleteEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken) => tableClient.DeleteEntityAsync(partitionKey, rowKey, cancellationToken: cancellationToken);

        public async Task<TableReadResponse<T>> GetEntityAsync<T>(string partitionKey, string rowKey, CancellationToken cancellationToken) where T : class, ITableEntity
        {
            try
            {
                global::Azure.Response<T> response = await tableClient.GetEntityAsync<T>(partitionKey, rowKey, cancellationToken: cancellationToken);
                return new TableReadResponse<T>(true, response.Value);
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                return new TableReadResponse<T>(false, null);
            }
        }

        public async IAsyncEnumerable<T> QueryEntitiesAsync<T>(string filter, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken) where T : class, ITableEntity
        {
            await foreach (T entity in tableClient.QueryAsync<T>(filter, cancellationToken: cancellationToken))
            {
                yield return entity;
            }
        }
    }

    private sealed class DefaultCosmosContainerAdapter(CosmosClient client, CosmosContainerDbConfig config) : ICosmosContainerAdapter
    {
        private Container GetContainer()
        {
            return client.GetDatabase(config.DatabaseName).GetContainer(config.ContainerName);
        }

        public async Task ValidateAccountReachabilityAsync(CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, client.Endpoint);
            using HttpResponseMessage _ = await DefaultHttpComponentFactory.SharedClient.SendAsync(request, cancellationToken);
        }

        public async Task ValidateAccountConnectionAsync(CancellationToken cancellationToken)
        {
            await client.ReadAccountAsync().WaitAsync(cancellationToken);
        }

        public async Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            using ResponseMessage response = await GetContainer().ReadContainerStreamAsync(cancellationToken: cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public async Task EnsureContainerExistsAsync<TItem>(TItem item, CancellationToken cancellationToken)
        {
            string partitionKeyPath = CosmosModelSchemaResolver.ResolvePartitionKeyPath<TItem>();
            await CosmosSchemaRestClient.EnsureDatabaseAndContainerExistAsync(config, partitionKeyPath, cancellationToken);
        }

        public async Task DeleteItemAsync<TItem>(string id, PartitionKey partitionKey, CancellationToken cancellationToken)
        {
            await GetContainer().DeleteItemAsync<TItem>(id, partitionKey, cancellationToken: cancellationToken);
        }

        public async Task UpsertItemAsync<TItem>(TItem item, PartitionKey partitionKey, CancellationToken cancellationToken)
        {
            await GetContainer().UpsertItemAsync(item, partitionKey, cancellationToken: cancellationToken);
        }

        public async Task<CosmosReadResponse> ReadItemAsync(string id, PartitionKey partitionKey, CancellationToken cancellationToken)
        {
            using ResponseMessage response = await GetContainer().ReadItemStreamAsync(id, partitionKey, cancellationToken: cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new CosmosReadResponse(false, null);
            }

            response.EnsureSuccessStatusCode();

            MemoryStream buffer = new MemoryStream();
            await response.Content.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return new CosmosReadResponse(true, buffer);
        }

        public async IAsyncEnumerable<TItem> QueryItemsAsync<TItem>(QueryDefinition query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            FeedIterator<TItem> iterator = GetContainer().GetItemQueryIterator<TItem>(query);
            while (iterator.HasMoreResults)
            {
                foreach (TItem item in await iterator.ReadNextAsync(cancellationToken))
                {
                    yield return item;
                }
            }
        }
    }

    private sealed class DefaultServiceBusComponentFactory(AzureClientCache clientCache) : IServiceBusComponentFactory
    {
        // The ServiceBusClient owns the AMQP connection and is shared across every step in the run.
        // The senders, processors and pumps built from it stay per-operation and are still disposed.
        private ServiceBusClient GetClient(ServiceBusConfig config)
            => clientCache.GetOrAdd(config.ConnectionString, () => new ServiceBusClient(config.ConnectionString));

        public IServiceBusSenderAdapter CreateSender(Resolved<ServiceBusConfig> serviceBus)
        {
            ServiceBusConfig config = serviceBus.Value;
            return new DefaultServiceBusSenderAdapter(GetClient(config), config);
        }

        public IServiceBusMessagePump CreateMessagePump(Resolved<ServiceBusConfig> serviceBus, string? subscriptionName)
        {
            ServiceBusConfig config = serviceBus.Value;
            return new DefaultServiceBusMessagePump(GetClient(config), config, subscriptionName);
        }

        public IServiceBusAdministrationAdapter CreateAdministration(Resolved<ServiceBusConfig> serviceBus)
        {
            ServiceBusConfig config = serviceBus.Value;
            return new DefaultServiceBusAdministrationAdapter(
                clientCache.GetOrAdd(config.ConnectionString, () => new ServiceBusAdministrationClient(config.ConnectionString)),
                config);
        }
    }

    private sealed class DefaultServiceBusSenderAdapter : IServiceBusSenderAdapter
    {
        private readonly ServiceBusSender _sender;

        public DefaultServiceBusSenderAdapter(ServiceBusClient client, ServiceBusConfig config)
        {
            _sender = client.CreateSender(config.EntityName);
        }

        public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken)
        {
            return _sender.SendMessageAsync(message, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            // Only the sender is ours. The ServiceBusClient is shared and owned by AzureClientCache -
            // disposing it here would break every later Service Bus step in the same run.
            await _sender.DisposeAsync();
        }
    }

    private sealed class DefaultServiceBusMessagePump : IServiceBusMessagePump
    {
        private readonly ServiceBusProcessor? _processor;
        private readonly ServiceBusSessionProcessor? _sessionProcessor;
        private int _abandonWarningIssued;
        private int _receiving;

        public DefaultServiceBusMessagePump(ServiceBusClient client, ServiceBusConfig config, string? subscriptionName)
        {
            // Queue first, as the receive mode decides: the entry names one entity, and this asks the same question
            // the mode did rather than a second one that could answer differently.
            if (config.RequiredSession)
            {
                _sessionProcessor = config.IsQueue
                    ? client.CreateSessionProcessor(config.QueueName, new ServiceBusSessionProcessorOptions { AutoCompleteMessages = false })
                    : client.CreateSessionProcessor(config.TopicName, subscriptionName, new ServiceBusSessionProcessorOptions { AutoCompleteMessages = false });
            }
            else
            {
                _processor = config.IsQueue
                    ? client.CreateProcessor(config.QueueName, new ServiceBusProcessorOptions { AutoCompleteMessages = false })
                    : client.CreateProcessor(config.TopicName, subscriptionName, new ServiceBusProcessorOptions { AutoCompleteMessages = false });
            }
        }

        public async Task<ServiceBusReceivedMessage> ReceiveMessageAsync(ServiceBusReceiveRequest request, CancellationToken cancellationToken)
        {
            // Handlers attach to the processor, so a second receive would add a second pair beside the first and both
            // would settle every message. A pump serves one wait.
            if (Interlocked.Exchange(ref _receiving, 1) != 0)
                throw new TestFramework.Core.Exceptions.FrameworkStateException("A Service Bus message pump serves one wait; create another for the next one.");

            TaskCompletionSource<ServiceBusReceivedMessage> completionSource = new();

            if (_sessionProcessor is not null)
            {
                _sessionProcessor.ProcessMessageAsync += args => HandleMessageAsync(
                    request,
                    completionSource,
                    args.Message,
                    (message, token) => args.CompleteMessageAsync(message, token),
                    (message, token) => args.AbandonMessageAsync(message, null, token),
                    cancellationToken);
                _sessionProcessor.ProcessErrorAsync += args => OnProcessorError(request, completionSource, args.Exception);

                await _sessionProcessor.StartProcessingAsync(cancellationToken);
                return await completionSource.Task.WaitAsync(cancellationToken);
            }

            if (_processor is null)
            {
                throw new TestFramework.Core.Exceptions.FrameworkStateException("No Service Bus processor was created.");
            }

            _processor.ProcessMessageAsync += args => HandleMessageAsync(
                request,
                completionSource,
                args.Message,
                (message, token) => args.CompleteMessageAsync(message, token),
                (message, token) => args.AbandonMessageAsync(message, null, token),
                cancellationToken);
            _processor.ProcessErrorAsync += args => OnProcessorError(request, completionSource, args.Exception);

            await _processor.StartProcessingAsync(cancellationToken);
            return await completionSource.Task.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Settles one delivered message. Shared by the queue and session handlers.
        /// </summary>
        /// <remarks>
        /// Two ordering rules matter here. A non-matching message is abandoned rather than simply
        /// ignored, because an ignored message keeps its lock (the SDK renews it for minutes) and
        /// starves concurrent runs. A matching message is completed BEFORE the waiter is signalled,
        /// because signalling first lets the waiter return and dispose the pump underneath the
        /// in-flight settlement.
        /// </remarks>
        private async Task HandleMessageAsync(
            ServiceBusReceiveRequest request,
            TaskCompletionSource<ServiceBusReceivedMessage> completionSource,
            ServiceBusReceivedMessage message,
            Func<ServiceBusReceivedMessage, CancellationToken, Task> completeAsync,
            Func<ServiceBusReceivedMessage, CancellationToken, Task> abandonAsync,
            CancellationToken cancellationToken)
        {
            bool matches = ServiceBusSettlement.Matches(request, message.MessageId, message.CorrelationId, predicate => predicate(message));

            switch (ServiceBusSettlement.Decide(request, matches))
            {
                case ServiceBusSettlementAction.Ignore:
                    return;

                case ServiceBusSettlementAction.Abandon:
                    if (Interlocked.Exchange(ref _abandonWarningIssued, 1) == 0)
                    {
                        request.Warn?.Invoke(
                            "Abandoning a non-matching Service Bus message so its lock is released for concurrent runs. " +
                            "This increases the message's DeliveryCount and moves it closer to dead-lettering.");
                    }

                    await abandonAsync(message, cancellationToken);
                    return;

                case ServiceBusSettlementAction.Complete:
                    await completeAsync(message, cancellationToken);
                    completionSource.TrySetResult(message);
                    return;

                case ServiceBusSettlementAction.AcceptWithoutCompleting:
                    completionSource.TrySetResult(message);
                    return;
            }
        }

        private static Task OnProcessorError(ServiceBusReceiveRequest request, TaskCompletionSource<ServiceBusReceivedMessage> completionSource, Exception exception)
        {
            if (ServiceBusSettlement.EndsTheWait(exception))
                completionSource.TrySetException(exception);
            else
                request.Warn?.Invoke($"A transient Service Bus error was reported and the processor retries it: {exception.Message}");

            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_sessionProcessor is not null)
            {
                await _sessionProcessor.DisposeAsync();
            }
            if (_processor is not null)
            {
                await _processor.DisposeAsync();
            }

            // The ServiceBusClient is shared and owned by AzureClientCache - see the sender adapter.
        }
    }

    private sealed class DefaultServiceBusAdministrationAdapter : IServiceBusAdministrationAdapter
    {
        private readonly ServiceBusAdministrationClient _client;
        private readonly ServiceBusConfig _config;

        public DefaultServiceBusAdministrationAdapter(ServiceBusAdministrationClient client, ServiceBusConfig config)
        {
            _config = config;
            _client = client;
        }

        public Task ValidateNamespaceConnectionAsync(CancellationToken cancellationToken)
        {
            return _client.GetNamespacePropertiesAsync(cancellationToken);
        }

        public async Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            if (_config.IsTopic)
            {
                await _client.GetTopicRuntimePropertiesAsync(_config.TopicName!, cancellationToken);
                return;
            }

            await _client.GetQueueRuntimePropertiesAsync(_config.QueueName!, cancellationToken);
        }

        public Task CreateSubscriptionAsync(CreateSubscriptionOptions options, CreateRuleOptions? ruleOptions, CancellationToken cancellationToken)
        {
            return ruleOptions is null
                ? _client.CreateSubscriptionAsync(options, cancellationToken)
                : _client.CreateSubscriptionAsync(options, ruleOptions, cancellationToken);
        }

        public Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken)
        {
            return _client.DeleteSubscriptionAsync(topicName, subscriptionName, cancellationToken);
        }
    }

    private sealed class DefaultHttpComponentFactory : IHttpComponentFactory
    {
        /// <summary>
        /// The one HttpClient every Azure HTTP call goes through.
        /// </summary>
        /// <remarks>
        /// The overall timeout stays infinite because the step's CancellationToken is the real budget
        /// - a long-polling wait must not be cut short by the client. The handler bounds the parts
        /// that the step token cannot express well: <c>ConnectTimeout</c> so a black-holed host fails
        /// fast instead of hanging on the TCP handshake, and <c>PooledConnectionLifetime</c> so a
        /// long-lived test host picks up DNS changes (failovers, swapped slots) instead of pinning a
        /// stale address for the life of the process.
        /// </remarks>
        internal static readonly HttpClient SharedClient = new(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(15),
        })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        public IHttpRequestSender CreateSender()
        {
            return new DefaultHttpRequestSender(SharedClient);
        }
    }

    private sealed class DefaultHttpRequestSender(HttpClient client) : IHttpRequestSender
    {
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
    }
}