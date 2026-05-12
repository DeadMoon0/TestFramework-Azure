using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TestFramework.Azure.LogicApp;

internal static class LogicAppJson
{
    internal static string ExtractCallbackUrl(string payload)
    {
        JObject document = JObject.Parse(payload);
        string? callbackUrl = document["value"]?.Value<string>()
            ?? document["callbackUrl"]?.Value<string>();

        return !string.IsNullOrWhiteSpace(callbackUrl)
            ? callbackUrl
            : throw new InvalidOperationException("The Logic App callback URL response did not contain a callback URL.");
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
        JObject document = JObject.Parse(payload);
        JToken? properties = document["properties"];
        string? statusText = properties?["status"]?.Value<string>() ?? document["status"]?.Value<string>();
        string? code = properties?["code"]?.Value<string>() ?? document["code"]?.Value<string>();
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
        JToken document = JToken.Parse(payload);
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
            string? statusText = properties?["status"]?.Value<string>() ?? run["status"]?.Value<string>();
            string? code = properties?["code"]?.Value<string>() ?? run["code"]?.Value<string>();
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