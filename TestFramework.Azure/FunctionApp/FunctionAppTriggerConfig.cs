using System;
using System.Net.Http;

namespace TestFramework.Azure.FunctionApp;

/// <summary>
/// Runtime options that influence how remote Function App triggers are executed.
/// </summary>
public record FunctionAppTriggerConfig
{
    /// <summary>
    /// When true, remote Function App triggers send an ICMP echo to the host before invoking the function.
    /// </summary>
    /// <remarks>
    /// Off by default, and only meaningful for self-hosted targets. Azure endpoints never answer ICMP,
    /// so against Azure this can only ever cost the probe timeout and then fail; ICMP also needs a
    /// raw-socket capability that many Linux CI containers do not grant. Liveness checks use an HTTP
    /// probe instead - see <see cref="ReachabilityProbeTimeout"/>.
    /// </remarks>
    public bool DoPing { get; set; }

    /// <summary>
    /// How long localhost-based Function App calls should tolerate transient 404 responses during host warm-up.
    /// </summary>
    public TimeSpan LocalNotFoundRetryDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Delay between localhost Function App retry attempts when the route is not yet ready.
    /// </summary>
    public TimeSpan LocalNotFoundRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long the HTTP reachability probe waits before declaring a host unreachable.
    /// </summary>
    /// <remarks>
    /// Required, because the shared HttpClient runs with an infinite timeout - the step's
    /// CancellationToken is the overall budget - so a black-holed host would otherwise hang here
    /// far longer than the old ICMP probe did.
    /// </remarks>
    public TimeSpan ReachabilityProbeTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Default HTTP method used when a function trigger does not expose one explicitly.
    /// </summary>
    public HttpMethod DefaultMethod { get; set; } = HttpMethod.Get;
}