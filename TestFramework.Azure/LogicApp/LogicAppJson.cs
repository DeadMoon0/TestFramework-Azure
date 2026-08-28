using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Json;

namespace TestFramework.Azure.LogicApp;

/// <remarks>
/// Every parse here goes through the engine's <see cref="WireJson"/>: these payloads are Azure's, and a
/// startTime read as a string has to still be a string when it is read back. <c>JObject.Parse</c> turned
/// ISO timestamps into dates, so the "raw" JSON handed onward was re-serialized in .NET's format rather
/// than being what Azure sent.
/// </remarks>
internal static class LogicAppJson
{
    internal static string ExtractCallbackUrl(string payload)
    {
        JObject? document = WireJson.Parse(payload) as JObject;
        string? callbackUrl = document?["value"]?.Value<string>()
            ?? document?["callbackUrl"]?.Value<string>();

        return !string.IsNullOrWhiteSpace(callbackUrl)
            ? callbackUrl
            : throw new TestFramework.Core.Exceptions.FrameworkStateException("The Logic App callback URL response did not contain a callback URL.");
    }

    internal static bool TryExtractCallbackUrl(string payload, out string? callbackUrl)
    {
        callbackUrl = null;

        if (string.IsNullOrWhiteSpace(payload))
            return false;

        try
        {
            callbackUrl = ExtractCallbackUrl(payload);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (TestFramework.Core.Exceptions.FrameworkStateException)
        {
            return false;
        }
    }

    internal static string RebaseLocalCallbackUrl(string baseUrl, string callbackUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri)
            || !Uri.TryCreate(callbackUrl, UriKind.Absolute, out Uri? callbackUri)
            || !callbackUri.IsLoopback)
        {
            return callbackUrl;
        }

        if (string.Equals(baseUri.Scheme, callbackUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(baseUri.Host, callbackUri.Host, StringComparison.OrdinalIgnoreCase)
            && baseUri.Port == callbackUri.Port)
        {
            return callbackUrl;
        }

        UriBuilder builder = new(callbackUri)
        {
            Scheme = baseUri.Scheme,
            Host = baseUri.Host,
            Port = baseUri.Port,
        };

        return builder.Uri.ToString();
    }

    internal static LogicAppRunDetails ParseRunDetails(string workflowName, string runId, string payload)
    {
        JObject document = WireJson.Parse(payload) as JObject
            ?? throw new JsonReaderException("The Logic App run payload was not a JSON object.");
        if (document["value"] is not null)
        {
            LogicAppRunDetails? fromRunsList = TryParseRunDetailsFromRunsList(workflowName, runId, payload);
            if (fromRunsList is not null)
                return fromRunsList;
        }

        JToken? properties = document["properties"];
        string? statusText = ResolveStatusText(properties, document);
        string? code = ResolveCode(properties, document);
        JToken? outputs = properties?["outputs"] ?? document["outputs"];

        return new LogicAppRunDetails(
            workflowName,
            runId,
            ParseStatus(statusText),
            code,
            outputs?.ToString(Newtonsoft.Json.Formatting.None),
            payload);
    }

    internal static LogicAppRunDetails? TryParseRunDetailsFromRunsList(string workflowName, string runId, string payload)
    {
        JToken document = WireJson.Parse(payload);
        IEnumerable<JToken> runs = document.Type switch
        {
            JTokenType.Array => document.Children(),
            JTokenType.Object => document["value"]?.Children() ?? Enumerable.Empty<JToken>(),
            _ => Enumerable.Empty<JToken>(),
        };

        foreach (JToken run in runs)
        {
            string? currentRunId = run["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(currentRunId))
            {
                string? id = run["id"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    string[] parts = id.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    currentRunId = parts.LastOrDefault();
                }
            }

            if (!string.Equals(currentRunId, runId, StringComparison.OrdinalIgnoreCase))
                continue;

            JToken? properties = run["properties"];
            string? statusText = ResolveStatusText(properties, run);
            string? code = ResolveCode(properties, run);
            JToken? outputs = properties?["outputs"] ?? run["outputs"];

            return new LogicAppRunDetails(
                workflowName,
                runId,
                ParseStatus(statusText),
                code,
                outputs?.ToString(Newtonsoft.Json.Formatting.None),
                run.ToString(Newtonsoft.Json.Formatting.None));
        }

        return null;
    }

    private static string? ResolveStatusText(JToken? properties, JToken root)
    {
        string? statusText = properties?["status"]?.Value<string>() ?? root["status"]?.Value<string>();
        LogicAppRunStatus status = ParseStatus(statusText);
        if (IsTerminal(status))
            return statusText;

        string? responseStatusText = properties?["response"]?["status"]?.Value<string>()
            ?? root["response"]?["status"]?.Value<string>();
        LogicAppRunStatus responseStatus = ParseStatus(responseStatusText);

        return IsTerminal(responseStatus) ? responseStatusText : statusText;
    }

    private static string? ResolveCode(JToken? properties, JToken root)
    {
        return properties?["code"]?.Value<string>()
            ?? root["code"]?.Value<string>()
            ?? properties?["response"]?["code"]?.Value<string>()
            ?? root["response"]?["code"]?.Value<string>()
            ?? properties?["response"]?["error"]?["code"]?.Value<string>()
            ?? root["response"]?["error"]?["code"]?.Value<string>();
    }

    internal static LogicAppRunStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return LogicAppRunStatus.Unknown;

        return value.Trim().ToLowerInvariant() switch
        {
            "running" => LogicAppRunStatus.Running,
            "waiting" => LogicAppRunStatus.Waiting,
            "succeeded" => LogicAppRunStatus.Succeeded,
            "failed" => LogicAppRunStatus.Failed,
            "cancelled" => LogicAppRunStatus.Cancelled,
            "timedout" => LogicAppRunStatus.TimedOut,
            "aborted" => LogicAppRunStatus.Aborted,
            "skipped" => LogicAppRunStatus.Skipped,
            _ => LogicAppRunStatus.Unknown,
        };
    }

    internal static bool IsTerminal(LogicAppRunStatus status)
        => status is LogicAppRunStatus.Succeeded or LogicAppRunStatus.Failed or LogicAppRunStatus.Cancelled or LogicAppRunStatus.TimedOut or LogicAppRunStatus.Aborted or LogicAppRunStatus.Skipped;
}