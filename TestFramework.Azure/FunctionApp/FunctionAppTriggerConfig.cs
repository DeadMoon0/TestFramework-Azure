using System;
using System.Net.Http;

namespace TestFramework.Azure.FunctionApp;

/// <summary>
/// Runtime options that influence how remote Function App triggers are executed.
/// </summary>
public record FunctionAppTriggerConfig
{
    /// <summary>
    /// When true, remote Function App triggers perform a host ping before invoking the function.
    /// </summary>
    public bool DoPing { get; set; } = true;

    /// <summary>
    /// How long localhost-based Function App calls should tolerate transient 404 responses during host warm-up.
    /// </summary>
    public TimeSpan LocalNotFoundRetryDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Delay between localhost Function App retry attempts when the route is not yet ready.
    /// </summary>
    public TimeSpan LocalNotFoundRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Default HTTP method used when a function trigger does not expose one explicitly.
    /// </summary>
    public HttpMethod DefaultMethod { get; set; } = HttpMethod.Get;
}