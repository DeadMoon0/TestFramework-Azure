using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Exceptions;
using TestFramework.Azure.LogicApp;
using TestFramework.Azure.Runtime;

namespace TestFramework.Azure.FunctionApp.TriggerConfigs;

internal record RemoteConnection(Uri BasePath)
{
    /// <summary>
    /// The address as it may be written down: in a log line, in a failure a run keeps.
    /// </summary>
    /// <remarks>
    /// A Consumption Logic App entry that only has invoke access is probed at its invoke URL, and the
    /// <c>sig</c> on that URL is the credential. Every message this connection produces uses this form, so
    /// the probe still reaches the real address while nothing it writes carries the signature.
    /// </remarks>
    internal string DisplayPath => LogicAppUriRedaction.Redact(BasePath.ToString())!;

    /// <summary>
    /// ICMP echo against the host. Only useful for self-hosted targets: Azure's front doors do not
    /// answer ICMP at all, and many Linux CI containers lack the raw-socket capability it needs.
    /// </summary>
    /// <param name="probeTimeout">
    /// How long to wait for the echo. A non-positive value falls back to the OS-typical five seconds,
    /// because an ICMP echo cannot be unbounded.
    /// </param>
    /// <param name="cancellationToken">The step's cancellation token.</param>
    internal async Task EnsurePingAsync(TimeSpan probeTimeout, CancellationToken cancellationToken)
    {
        using Ping pingSender = new Ping();

        TimeSpan timeout = probeTimeout > TimeSpan.Zero ? probeTimeout : TimeSpan.FromSeconds(5);
        PingReply reply = await pingSender.SendPingAsync(BasePath.Host, timeout, null, null, cancellationToken);
        if (reply.Status != IPStatus.Success)
        {
            throw new FunctionAppPingException("Ping was not Successful with Status: " + reply.Status, null);
        }
    }

    /// <summary>
    /// Verifies that the host answers HTTP at all. Any status code counts as reachable - a 401, 404
    /// or 403 still proves something is listening and terminating TLS at the other end.
    /// </summary>
    /// <param name="sender">The shared HTTP sender.</param>
    /// <param name="probeTimeout">How long to wait before declaring the host unreachable.</param>
    /// <param name="cancellationToken">The step's cancellation token.</param>
    /// <exception cref="FunctionAppPingException">Thrown when the host cannot be reached at the transport level or does not answer in time.</exception>
    internal async Task EnsureReachableAsync(IHttpRequestSender sender, TimeSpan probeTimeout, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (probeTimeout > TimeSpan.Zero)
        {
            timeoutSource.CancelAfter(probeTimeout);
        }

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Head, BasePath);
            using HttpResponseMessage _ = await sender.SendAsync(request, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FunctionAppPingException(
                $"'{DisplayPath}' did not answer an HTTP probe within {probeTimeout}. The host is unreachable or is black-holing traffic.",
                null);
        }
        catch (HttpRequestException exception)
        {
            throw new FunctionAppPingException(
                $"'{DisplayPath}' could not be reached over HTTP: {LogicAppUriRedaction.Redact(exception.Message)}",
                exception);
        }
    }
}
