using System;

namespace TestFramework.Azure.Runtime;

/// <summary>
/// Recognises endpoints that belong to a local development host (emulators, locally hosted
/// Function App runtimes, containers reached through the Docker host alias).
/// </summary>
/// <remarks>
/// This is the single place that decides whether relaxed behaviour - most importantly skipping
/// TLS certificate validation for the self-signed emulator certificates - may be applied. Any
/// endpoint that is not recognised here is treated as a real, remote Azure endpoint.
/// </remarks>
internal static class LocalEndpoints
{
    /// <summary>
    /// Determines whether the URI addresses a local development host.
    /// </summary>
    /// <param name="uri">The endpoint to classify.</param>
    /// <returns><see langword="true"/> for loopback, <c>localhost</c> and <c>host.docker.internal</c>.</returns>
    internal static bool IsLocalDevelopmentHost(Uri uri)
    {
        if (uri is null)
            return false;

        return uri.IsLoopback
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "host.docker.internal", StringComparison.OrdinalIgnoreCase);
    }
}
