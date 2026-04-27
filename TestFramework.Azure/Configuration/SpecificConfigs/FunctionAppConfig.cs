namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// Configuration required to call a remote Azure Function App.
/// </summary>
/// <remarks>
/// The identifier maps to a named entry under the <c>FunctionApp</c> section.
/// <see cref="BaseUrl"/> and <see cref="Code"/> are required for all remote calls.
/// </remarks>
public record FunctionAppConfig
{
    /// <summary>
    /// Absolute Function App host URL, for example <c>https://my-app.azurewebsites.net/</c>.
    /// </summary>
    public required string BaseUrl { get; init; }

    /// <summary>
    /// Function key used for normal trigger invocations.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Optional admin key used for host-level health checks. When absent, <see cref="Code"/> is reused.
    /// </summary>
    public string? AdminCode { get; init; }
}