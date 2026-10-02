namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// Configuration required to call a remote Azure Function App.
/// </summary>
/// <remarks>
/// The identifier maps to a named entry under the <c>FunctionApp</c> section.
/// <see cref="BaseUrl"/> is required for all remote calls. <see cref="Code"/> is the function key: leave it out, or
/// empty, for a Function App that takes none - an anonymous or a locally hosted one - and no key is sent.
/// </remarks>
public sealed record FunctionAppConfig
{
    // Its text redacts its credentials; see ConfigRecordText.
    private bool PrintMembers(System.Text.StringBuilder builder) => ConfigRecordText.PrintMembers(this, builder);

    /// <summary>
    /// Absolute Function App SITE ROOT, for example <c>https://my-app.azurewebsites.net/</c>.
    /// </summary>
    /// <remarks>
    /// This is the host root, not the API root: the HTTP route prefix belongs in
    /// <see cref="RoutePrefix"/>, and host-level endpoints such as <c>admin/host/status</c> are
    /// resolved against this value directly.
    /// </remarks>
    public required string BaseUrl { get; init; }

    /// <summary>
    /// The host's HTTP route prefix, matching <c>extensions.http.routePrefix</c> in <c>host.json</c>.
    /// </summary>
    /// <remarks>
    /// Defaults to the Functions host default of <c>api</c>. Set it to an empty string for a host
    /// configured with <c>"routePrefix": ""</c>. Every HTTP route the builders produce - a custom
    /// <c>[HttpTrigger(Route = ...)]</c>, a plain function name, or an explicit sub-path - is relative
    /// to this prefix, so the prefix is applied exactly once, here.
    /// </remarks>
    public string RoutePrefix { get; init; } = "api";

    /// <summary>
    /// Function key used for normal trigger invocations.
    /// </summary>
    [Secret]
    public string? Code
    {
        get => code;
        init => code = NoKeyIfEmpty(value);
    }

    /// <summary>
    /// Optional admin key used for host-level health checks. When absent, <see cref="Code"/> is reused.
    /// </summary>
    [Secret]
    public string? AdminCode
    {
        get => adminCode;
        init => adminCode = NoKeyIfEmpty(value);
    }

    private readonly string? code;
    private readonly string? adminCode;

    // An empty key in a settings file means "no key", and is held as one: two spellings of one state would leave
    // every reader to agree on it, and the one that did not sent an empty x-functions-key header.
    private static string? NoKeyIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}