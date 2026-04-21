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
    IBlobContainerAdapter CreateContainer(StorageAccountConfig config);
}

internal interface IBlobContainerAdapter
{
    Task ValidateConnectionAsync(CancellationToken cancellationToken);
    Task CreateIfNotExistsAsync();
    Task DeleteBlobAsync(string path);
    Task UploadBlobAsync(string path, byte[] data, IReadOnlyDictionary<string, string> metadata);
    Task<BlobReadResponse> ReadBlobAsync(string path);
}

internal readonly record struct BlobReadResponse(bool Found, byte[]? Data, IReadOnlyDictionary<string, string>? Metadata);

internal interface ITableComponentFactory
{
    ITableAdapter CreateTable(StorageAccountConfig config, string tableName);
}

internal interface ITableAdapter
{
    Task ValidateConnectionAsync(CancellationToken cancellationToken);
    Task CreateIfNotExistsAsync();
    Task UpsertEntityAsync<T>(T entity) where T : class, ITableEntity;
    Task DeleteEntityAsync(string partitionKey, string rowKey);
    Task<TableReadResponse<T>> GetEntityAsync<T>(string partitionKey, string rowKey) where T : class, ITableEntity;
    IAsyncEnumerable<T> QueryEntitiesAsync<T>(string filter, CancellationToken cancellationToken) where T : class, ITableEntity;
}

internal readonly record struct TableReadResponse<T>(bool Found, T? Entity) where T : class, ITableEntity;

internal interface ICosmosComponentFactory
{
    ICosmosContainerAdapter CreateContainer(CosmosContainerDbConfig config);
}

internal interface ICosmosContainerAdapter
{
    Task ValidateConnectionAsync(CancellationToken cancellationToken);
    Task DeleteItemAsync<TItem>(string id, PartitionKey partitionKey);
    Task UpsertItemAsync<TItem>(TItem item, PartitionKey partitionKey);
    Task<CosmosReadResponse> ReadItemAsync(string id, PartitionKey partitionKey);
    IAsyncEnumerable<TItem> QueryItemsAsync<TItem>(QueryDefinition query, CancellationToken cancellationToken);
}

internal readonly record struct CosmosReadResponse(bool Found, Stream? Content);

internal interface IServiceBusComponentFactory
{
    IServiceBusSenderAdapter CreateSender(ServiceBusConfig config);
    IServiceBusMessagePump CreateMessagePump(ServiceBusConfig config, string? subscriptionName);
    IServiceBusAdministrationAdapter CreateAdministration(ServiceBusConfig config);
}

internal interface IServiceBusSenderAdapter : IAsyncDisposable
{
    Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken);
}

internal readonly record struct ServiceBusReceiveRequest(
    string? MessageId,
    string? CorrelationId,
    Func<ServiceBusReceivedMessage, bool>? Predicate,
    bool CompleteMessage);

internal interface IServiceBusMessagePump : IAsyncDisposable
{
    Task<ServiceBusReceivedMessage> ReceiveMessageAsync(ServiceBusReceiveRequest request, CancellationToken cancellationToken);
}

internal interface IServiceBusAdministrationAdapter
{
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
    public IBlobComponentFactory Blob { get; } = new DefaultBlobComponentFactory();
    public ITableComponentFactory Table { get; } = new DefaultTableComponentFactory();
    public IServiceBusComponentFactory ServiceBus { get; } = new DefaultServiceBusComponentFactory();
    public IHttpComponentFactory Http { get; } = new DefaultHttpComponentFactory();

    private readonly IServiceProvider _serviceProvider;

    internal DefaultAzureComponentFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Cosmos = new DefaultCosmosComponentFactory(_serviceProvider);
    }

    private sealed class DefaultCosmosComponentFactory(IServiceProvider serviceProvider) : ICosmosComponentFactory
    {
        public ICosmosContainerAdapter CreateContainer(CosmosContainerDbConfig config)
        {
            CosmosClientOptions options = CosmosClientOptionsResolver.Resolve(serviceProvider, config);
            CosmosClient client = new CosmosClient(config.ConnectionString, options);
            Container container = client.GetDatabase(config.DatabaseName).GetContainer(config.ContainerName);
            return new DefaultCosmosContainerAdapter(container);
        }
    }

    private sealed class DefaultBlobComponentFactory : IBlobComponentFactory
    {
        public IBlobContainerAdapter CreateContainer(StorageAccountConfig config)
        {
            BlobServiceClient client = new BlobServiceClient(config.ConnectionString);
            BlobContainerClient container = client.GetBlobContainerClient(config.BlobContainerNameRequired);
            return new DefaultBlobContainerAdapter(container);
        }
    }

    private sealed class DefaultBlobContainerAdapter(BlobContainerClient container) : IBlobContainerAdapter
    {
        public async Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            if (!await container.ExistsAsync(cancellationToken))
            {
                throw new InvalidOperationException($"Blob container '{container.Name}' was not found or is not accessible.");
            }
        }

        public Task CreateIfNotExistsAsync() => container.CreateIfNotExistsAsync();

        public Task DeleteBlobAsync(string path) => container.DeleteBlobAsync(path, DeleteSnapshotsOption.IncludeSnapshots);

        public async Task UploadBlobAsync(string path, byte[] data, IReadOnlyDictionary<string, string> metadata)
        {
            BlobClient blob = container.GetBlobClient(path);
            await blob.UploadAsync(new BinaryData(data), true);
            await blob.SetMetadataAsync(new Dictionary<string, string>(metadata));
        }

        public async Task<BlobReadResponse> ReadBlobAsync(string path)
        {
            BlobClient blob = container.GetBlobClient(path);
            if (!await blob.ExistsAsync())
            {
                return new BlobReadResponse(false, null, null);
            }

            BlobDownloadResult content = (await blob.DownloadContentAsync()).Value;
            BlobProperties properties = (await blob.GetPropertiesAsync()).Value;
            return new BlobReadResponse(true, content.Content.ToArray(), new Dictionary<string, string>(properties.Metadata));
        }
    }

    private sealed class DefaultTableComponentFactory : ITableComponentFactory
    {
        public ITableAdapter CreateTable(StorageAccountConfig config, string tableName)
        {
            TableServiceClient serviceClient = new TableServiceClient(config.ConnectionString);
            TableClient tableClient = serviceClient.GetTableClient(tableName);
            return new DefaultTableAdapter(tableClient);
        }
    }

    private sealed class DefaultTableAdapter(TableClient tableClient) : ITableAdapter
    {
        public async Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            await foreach (Page<TableEntity> _ in tableClient.QueryAsync<TableEntity>(maxPerPage: 1, cancellationToken: cancellationToken)
                .AsPages(pageSizeHint: 1))
            {
                break;
            }
        }

        public Task CreateIfNotExistsAsync() => tableClient.CreateIfNotExistsAsync();

        public Task UpsertEntityAsync<T>(T entity) where T : class, ITableEntity => tableClient.UpsertEntityAsync(entity);

        public Task DeleteEntityAsync(string partitionKey, string rowKey) => tableClient.DeleteEntityAsync(partitionKey, rowKey);

        public async Task<TableReadResponse<T>> GetEntityAsync<T>(string partitionKey, string rowKey) where T : class, ITableEntity
        {
            try
            {
                global::Azure.Response<T> response = await tableClient.GetEntityAsync<T>(partitionKey, rowKey);
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

    private sealed class DefaultCosmosContainerAdapter(Container container) : ICosmosContainerAdapter
    {
        public async Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            using ResponseMessage response = await container.ReadContainerStreamAsync(cancellationToken: cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteItemAsync<TItem>(string id, PartitionKey partitionKey)
        {
            await container.DeleteItemAsync<TItem>(id, partitionKey);
        }

        public async Task UpsertItemAsync<TItem>(TItem item, PartitionKey partitionKey)
        {
            await container.UpsertItemAsync(item, partitionKey);
        }

        public async Task<CosmosReadResponse> ReadItemAsync(string id, PartitionKey partitionKey)
        {
            using ResponseMessage response = await container.ReadItemStreamAsync(id, partitionKey);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new CosmosReadResponse(false, null);
            }

            response.EnsureSuccessStatusCode();

            MemoryStream buffer = new MemoryStream();
            await response.Content.CopyToAsync(buffer);
            buffer.Position = 0;
            return new CosmosReadResponse(true, buffer);
        }

        public async IAsyncEnumerable<TItem> QueryItemsAsync<TItem>(QueryDefinition query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            FeedIterator<TItem> iterator = container.GetItemQueryIterator<TItem>(query);
            while (iterator.HasMoreResults)
            {
                foreach (TItem item in await iterator.ReadNextAsync(cancellationToken))
                {
                    yield return item;
                }
            }
        }
    }

    private sealed class DefaultServiceBusComponentFactory : IServiceBusComponentFactory
    {
        public IServiceBusSenderAdapter CreateSender(ServiceBusConfig config)
        {
            return new DefaultServiceBusSenderAdapter(config);
        }

        public IServiceBusMessagePump CreateMessagePump(ServiceBusConfig config, string? subscriptionName)
        {
            return new DefaultServiceBusMessagePump(config, subscriptionName);
        }

        public IServiceBusAdministrationAdapter CreateAdministration(ServiceBusConfig config)
        {
            return new DefaultServiceBusAdministrationAdapter(config);
        }
    }

    private sealed class DefaultServiceBusSenderAdapter : IServiceBusSenderAdapter
    {
        private readonly ServiceBusClient _client;
        private readonly ServiceBusSender _sender;

        public DefaultServiceBusSenderAdapter(ServiceBusConfig config)
        {
            _client = new ServiceBusClient(config.ConnectionString);
            _sender = _client.CreateSender(config.EntityName);
        }

        public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken)
        {
            return _sender.SendMessageAsync(message, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _sender.DisposeAsync();
            await _client.DisposeAsync();
        }
    }

    private sealed class DefaultServiceBusMessagePump : IServiceBusMessagePump
    {
        private readonly ServiceBusClient _client;
        private readonly ServiceBusProcessor? _processor;
        private readonly ServiceBusSessionProcessor? _sessionProcessor;

        public DefaultServiceBusMessagePump(ServiceBusConfig config, string? subscriptionName)
        {
            _client = new ServiceBusClient(config.ConnectionString);
            if (config.RequiredSession)
            {
                _sessionProcessor = config.IsTopic
                    ? _client.CreateSessionProcessor(config.TopicName, subscriptionName, new ServiceBusSessionProcessorOptions { AutoCompleteMessages = false })
                    : _client.CreateSessionProcessor(config.EntityName, new ServiceBusSessionProcessorOptions { AutoCompleteMessages = false });
            }
            else
            {
                _processor = config.IsTopic
                    ? _client.CreateProcessor(config.TopicName, subscriptionName, new ServiceBusProcessorOptions { AutoCompleteMessages = false })
                    : _client.CreateProcessor(config.EntityName, new ServiceBusProcessorOptions { AutoCompleteMessages = false });
            }
        }

        public async Task<ServiceBusReceivedMessage> ReceiveMessageAsync(ServiceBusReceiveRequest request, CancellationToken cancellationToken)
        {
            TaskCompletionSource<ServiceBusReceivedMessage> completionSource = new();

            if (_sessionProcessor is not null)
            {
                _sessionProcessor.ProcessMessageAsync += async args =>
                {
                    if (request.MessageId is { } messageId && args.Message.MessageId != messageId) return;
                    if (request.CorrelationId is { } correlationId && args.Message.CorrelationId != correlationId) return;
                    if (request.Predicate is { } predicate && !predicate(args.Message)) return;
                    completionSource.TrySetResult(args.Message);
                    if (request.CompleteMessage)
                    {
                        await args.CompleteMessageAsync(args.Message, cancellationToken);
                    }
                };
                _sessionProcessor.ProcessErrorAsync += args =>
                {
                    completionSource.TrySetException(args.Exception);
                    return Task.CompletedTask;
                };

                await _sessionProcessor.StartProcessingAsync(cancellationToken);
                return await completionSource.Task.WaitAsync(cancellationToken);
            }

            if (_processor is null)
            {
                throw new InvalidOperationException("No Service Bus processor was created.");
            }

            _processor.ProcessMessageAsync += async args =>
            {
                if (request.MessageId is { } messageId && args.Message.MessageId != messageId) return;
                if (request.CorrelationId is { } correlationId && args.Message.CorrelationId != correlationId) return;
                if (request.Predicate is { } predicate && !predicate(args.Message)) return;
                completionSource.TrySetResult(args.Message);
                if (request.CompleteMessage)
                {
                    await args.CompleteMessageAsync(args.Message, cancellationToken);
                }
            };
            _processor.ProcessErrorAsync += args =>
            {
                completionSource.TrySetException(args.Exception);
                return Task.CompletedTask;
            };

            await _processor.StartProcessingAsync(cancellationToken);
            return await completionSource.Task.WaitAsync(cancellationToken);
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
            await _client.DisposeAsync();
        }
    }

    private sealed class DefaultServiceBusAdministrationAdapter : IServiceBusAdministrationAdapter
    {
        private readonly ServiceBusAdministrationClient _client;
        private readonly ServiceBusConfig _config;

        public DefaultServiceBusAdministrationAdapter(ServiceBusConfig config)
        {
            _config = config;
            _client = new ServiceBusAdministrationClient(config.ConnectionString);
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
        public IHttpRequestSender CreateSender()
        {
            return new DefaultHttpRequestSender();
        }
    }

    private sealed class DefaultHttpRequestSender : IHttpRequestSender
    {
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using HttpClient client = new HttpClient();
            return await client.SendAsync(request, cancellationToken);
        }
    }
}