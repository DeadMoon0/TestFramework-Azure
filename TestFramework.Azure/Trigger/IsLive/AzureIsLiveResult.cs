using System.Net;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.Trigger.IsLive;

/// <summary>
/// Structured result returned by Azure liveness triggers.
/// </summary>
/// <param name="ResourceKind">The canonical Azure resource kind that was checked.</param>
/// <param name="ResourceIdentifier">The resolved resource identifier that was checked.</param>
/// <param name="AlivenessLevel">The requested liveness depth.</param>
/// <param name="Success">Whether the check completed successfully.</param>
/// <param name="StatusCode">Optional HTTP status code observed during HTTP-backed checks.</param>
public sealed record AzureIsLiveResult(
    string ResourceKind,
    string ResourceIdentifier,
    AlivenessLevel AlivenessLevel,
    bool Success,
    HttpStatusCode? StatusCode) : StepResultContext
{
    /// <summary>
    /// Gets the known typed resource category when the resource kind matches a built-in Azure surface.
    /// </summary>
    public AzureKnownResourceKind KnownResourceKind => AzureKnownResourceKindResolver.FromIdentifier(ResourceKind);
}