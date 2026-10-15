using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.Extensions;
using TestFramework.Azure.Runtime;
using Xunit;

namespace TestFramework.Azure.Tests;

/// <summary>
/// The client cache lets go of its connections however its service provider is disposed.
/// </summary>
/// <remarks>
/// The damage without it: a test that disposed its provider with a plain <c>using</c> got an
/// <see cref="InvalidOperationException"/> from the container after it had passed, naming an internal type - or, not
/// disposing at all, leaked an AMQP connection and a Cosmos pool per test class.
/// </remarks>
public sealed class AzureClientCacheTests
{
    [Fact]
    public void AProviderDisposedSynchronously_DisposesTheClientsItCreated()
    {
        ServiceCollection services = new();
        services.AddAzure();
        ServiceProvider provider = services.BuildServiceProvider();

        AzureClientCache cache = provider.GetRequiredService<AzureClientCache>();
        SyncClient sync = cache.GetOrAdd("sync", () => new SyncClient());
        AsyncOnlyClient asyncOnly = cache.GetOrAdd("async", () => new AsyncOnlyClient());

        provider.Dispose();

        Assert.True(sync.Disposed);
        Assert.True(asyncOnly.Disposed);
    }

    [Fact]
    public async Task AProviderDisposedAsynchronously_StillDisposesThemOnce()
    {
        ServiceCollection services = new();
        services.AddAzure();
        ServiceProvider provider = services.BuildServiceProvider();

        AzureClientCache cache = provider.GetRequiredService<AzureClientCache>();
        SyncClient sync = cache.GetOrAdd("sync", () => new SyncClient());

        await provider.DisposeAsync();
        cache.Dispose();

        Assert.Equal(1, sync.DisposeCount);
    }

    private sealed class SyncClient : IDisposable
    {
        public int DisposeCount { get; private set; }

        public bool Disposed => DisposeCount > 0;

        public void Dispose() => DisposeCount++;
    }

    private sealed class AsyncOnlyClient : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
