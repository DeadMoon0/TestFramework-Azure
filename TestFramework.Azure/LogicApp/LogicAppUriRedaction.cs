using System;
using System.Collections.Generic;
using System.Text;

namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Strips the shared-access signature out of a Logic App callback URL.
/// </summary>
/// <remarks>
/// A Consumption callback URL is a bearer credential: anyone holding the <c>sig</c> value can invoke
/// the workflow. The URL reaches results, logs, reports and the DebugUI, so it is redacted at the
/// point where it becomes a result. The live invoke path keeps the unredacted URL in a local
/// variable. Hand-rolled on purpose - taking a System.Web dependency for this would also mean
/// <c>HttpUtility</c> re-encoding the parameters that survive.
/// </remarks>
internal static class LogicAppUriRedaction
{
    internal const string RedactedValue = "***redacted***";

    private static readonly HashSet<string> SensitiveParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "sig",
        "sp",
        "sv",
    };

    /// <summary>
    /// Returns <paramref name="url"/> with the signature parameters replaced.
    /// </summary>
    /// <param name="url">The URL to redact. May be <see langword="null"/>.</param>
    /// <returns>The redacted URL, or the input unchanged when it carries no query string.</returns>
    internal static string? Redact(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        int queryStart = url.IndexOf('?');
        if (queryStart < 0)
            return url;

        string prefix = url.Substring(0, queryStart);
        string query = url.Substring(queryStart + 1);

        // A fragment is not part of the query, so keep it verbatim.
        string fragment = string.Empty;
        int fragmentStart = query.IndexOf('#');
        if (fragmentStart >= 0)
        {
            fragment = query.Substring(fragmentStart);
            query = query.Substring(0, fragmentStart);
        }

        StringBuilder builder = new(url.Length + 16);
        builder.Append(prefix).Append('?');

        bool first = true;
        foreach (string pair in query.Split('&'))
        {
            if (!first)
                builder.Append('&');
            first = false;

            int separator = pair.IndexOf('=');
            if (separator < 0)
            {
                builder.Append(pair);
                continue;
            }

            string name = pair.Substring(0, separator);
            builder.Append(name).Append('=');
            builder.Append(SensitiveParameters.Contains(name) ? RedactedValue : pair.Substring(separator + 1));
        }

        return builder.Append(fragment).ToString();
    }

    /// <summary>
    /// Redacts an absolute URI, returning the redacted form as a string.
    /// </summary>
    /// <param name="uri">The URI to redact.</param>
    /// <returns>The redacted URL, or <see langword="null"/> when <paramref name="uri"/> is null.</returns>
    internal static string? Redact(Uri? uri) => uri is null ? null : Redact(uri.ToString());
}
