using System.Net.Http;
using System.Net.Security;
using System.Threading;

namespace TestFramework.Azure.Runtime;

/// <summary>
/// The HTTP clients every Azure HTTP call in this package goes through, built by one handler factory.
/// </summary>
/// <remarks>
/// <para>
/// Steps reach <see cref="Shared"/> through <see cref="IHttpComponentFactory"/>, the seam a test replaces. Cosmos
/// schema setup is reached from the public <c>CosmosSchema</c> surface, which has no run and so no factory, and
/// it needs the emulator variant the seam does not offer; it takes its client from here directly. Either way
/// there is one place that decides how a connection behaves.
/// </para>
/// <para>
/// The overall timeout stays infinite because the step's CancellationToken is the real budget - a long-polling
/// wait must not be cut short by the client. The handler bounds the parts that the step token cannot express
/// well: <c>ConnectTimeout</c> so a black-holed host fails fast instead of hanging on the TCP handshake, and
/// <c>PooledConnectionLifetime</c> so a long-lived test host picks up DNS changes (failovers, swapped slots)
/// instead of pinning a stale address for the life of the process.
/// </para>
/// </remarks>
internal static class AzureHttpClients
{
    /// <summary>
    /// The client for every real endpoint. Certificate validation stays on.
    /// </summary>
    internal static readonly HttpClient Shared = Create(acceptAnyServerCertificate: false);

    /// <summary>
    /// The client for local emulators only, which present a self-signed certificate. Pick it through
    /// <see cref="LocalEndpoints.IsLocalDevelopmentHost"/> and nothing else: a request it carries to a real
    /// account could be intercepted.
    /// </summary>
    internal static readonly HttpClient LocalEmulator = Create(acceptAnyServerCertificate: true);

    private static HttpClient Create(bool acceptAnyServerCertificate)
    {
        SocketsHttpHandler handler = new()
        {
            PooledConnectionLifetime = AzureTimings.HttpPooledConnectionLifetime,
            ConnectTimeout = AzureTimings.HttpConnectTimeout,
        };

        if (acceptAnyServerCertificate)
        {
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            };
        }

        return new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }
}
