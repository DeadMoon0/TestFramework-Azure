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
    /// Default HTTP method used when a function trigger does not expose one explicitly.
    /// </summary>
    public HttpMethod DefaultMethod { get; set; } = HttpMethod.Get;
}