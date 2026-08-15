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
/// </remarks>
internal sealed class AzureClientCache : IAsyncDisposable
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
        if (System.Threading.Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        List<object> entries = new(clients.Values);
        clients.Clear();

        foreach (object entry in entries)
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
}
