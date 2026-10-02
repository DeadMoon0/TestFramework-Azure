using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TestFramework.Azure.Runtime;

/// <summary>
/// Caches the long-lived Azure SDK clients for the lifetime of the service provider.
/// </summary>
/// <remarks>
/// Azure SDK clients are connection multiplexers, not per-call objects: <c>CosmosClient</c> in
/// particular is a documented singleton that owns a connection pool and, before this cache, was
/// created per operation and never disposed. Only the underlying clients live here - senders,
/// processors and pumps are still created per operation, because they carry per-operation state.
/// <para>
/// Disposable both ways, because the service provider that owns it may be disposed either way. A synchronous
/// <c>using</c> - the common shape in a test - used to throw from the container once a client existed, naming this
/// internal type and no fix, while the asynchronous road was the only one that worked.
/// </para>
/// </remarks>
internal sealed class AzureClientCache : IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<(Type ClientType, object Key), object> clients = new();
    private int disposed;

    /// <summary>
    /// Returns the cached client for <paramref name="key"/>, creating it on first use.
    /// </summary>
    /// <typeparam name="T">The client type; also part of the cache key.</typeparam>
    /// <param name="key">A value-equatable key identifying the client, typically a connection string or config record.</param>
    /// <param name="factory">Creates the client when the cache has no entry yet.</param>
    /// <returns>The cached client instance.</returns>
    internal T GetOrAdd<T>(object key, Func<T> factory) where T : class
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        return (T)clients.GetOrAdd((typeof(T), key), _ => factory());
    }

    /// <summary>
    /// Disposes every cached client. Registered as a singleton, so the DI container owns this.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (object entry in TakeEntries())
        {
            switch (entry)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }

    /// <summary>
    /// Disposes every cached client synchronously, for a service provider disposed with a plain <c>using</c>.
    /// </summary>
    /// <remarks>
    /// A client that is only asynchronously disposable - the Service Bus client - is waited for here. Its disposal
    /// closes AMQP links and does not need the caller's synchronization context, so blocking on it is safe; it is the
    /// one cost of disposing synchronously, and <c>await using</c> avoids it.
    /// </remarks>
    public void Dispose()
    {
        foreach (object entry in TakeEntries())
        {
            switch (entry)
            {
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
                case IAsyncDisposable asyncDisposable:
                    asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    break;
            }
        }
    }

    private List<object> TakeEntries()
    {
        if (System.Threading.Interlocked.Exchange(ref disposed, 1) != 0)
            return [];

        List<object> entries = new(clients.Values);
        clients.Clear();
        return entries;
    }
}
