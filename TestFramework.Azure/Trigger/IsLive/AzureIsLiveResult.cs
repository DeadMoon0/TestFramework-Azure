using System.Net;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.Trigger.IsLive;

/// <summary>
/// Structured result returned by Azure liveness triggers.
/// </summary>
/// <remarks>
/// A result exists only for a probe that answered: one that did not fails its step. So there is no success flag to
/// read - it would always say yes.
/// </remarks>
public sealed record AzureIsLiveResult : StepResultContext
{
    internal AzureIsLiveResult(string resourceKind, string resourceIdentifier, AlivenessLevel alivenessLevel, HttpStatusCode? statusCode)
    {
        this.ResourceKind = resourceKind;
        this.ResourceIdentifier = resourceIdentifier;
        this.AlivenessLevel = alivenessLevel;
        this.StatusCode = statusCode;
    }

    /// <summary>
    /// The canonical Azure resource kind that was checked.
    /// </summary>
    public string ResourceKind { get; }

    /// <summary>
    /// The resolved resource identifier that was checked.
    /// </summary>
    public string ResourceIdentifier { get; }

    /// <summary>
    /// The requested liveness depth.
    /// </summary>
    public AlivenessLevel AlivenessLevel { get; }

    /// <summary>
    /// Optional HTTP status code observed during HTTP-backed checks.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets the known typed resource category when the resource kind matches a built-in Azure surface.
    /// </summary>
    public AzureKnownResourceKind KnownResourceKind => AzureKnownResourceKindResolver.FromIdentifier(ResourceKind);
}