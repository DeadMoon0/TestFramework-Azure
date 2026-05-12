using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.LogicApp.Trigger;

internal static class LogicAppHttpTriggerExecution
{
    internal static async Task<LogicAppHttpInvokeResponse> InvokeAsync(
        IServiceProvider serviceProvider,
        VariableStore variableStore,
        LogicAppIdentifier identifier,
        VariableReference<string>? workflowName,
        VariableReference<string> triggerName,
        VariableReference<CommonHttpRequest> request,
        CancellationToken cancellationToken)
    {
        LogicAppConfig config = serviceProvider.GetRequiredService<ConfigStore<LogicAppConfig>>().GetConfig(identifier);
        string resolvedWorkflowName = LogicAppRoutes.ResolveWorkflowName(config, workflowName?.GetValue(variableStore));
        string resolvedTriggerName = triggerName.GetRequiredValue(variableStore);
        CommonHttpRequest resolvedRequest = request.GetRequiredValue(variableStore);
        IHttpRequestSender sender = serviceProvider.GetAzureComponentFactory().Http.CreateSender();
        string callbackUrl;
        if (config.HostingMode == LogicAppHostingMode.Consumption)
        {
            callbackUrl = LogicAppRoutes.BuildListCallbackUrlUri(config, resolvedWorkflowName, resolvedTriggerName).ToString();
        }
        else
        {
            using HttpRequestMessage callbackUrlRequest = LogicAppRoutes.CreateManagementRequest(
                HttpMethod.Post,
                LogicAppRoutes.BuildListCallbackUrlUri(config, resolvedWorkflowName, resolvedTriggerName),
                config);
            callbackUrlRequest.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            using HttpResponseMessage callbackUrlResponse = await sender.SendAsync(callbackUrlRequest, cancellationToken);
            callbackUrlResponse.EnsureSuccessStatusCode();
            string callbackUrlPayload = await (callbackUrlResponse.Content?.ReadAsStringAsync(cancellationToken) ?? Task.FromResult(string.Empty));
            callbackUrl = LogicAppJson.RebaseLocalCallbackUrl(LogicAppRoutes.RequireStandardBaseUrl(config), LogicAppJson.ExtractCallbackUrl(callbackUrlPayload));
        }

        using HttpRequestMessage invokeRequest = new(HttpMethod.Post, callbackUrl);
        resolvedRequest.ApplyToHttpRequestMessage(invokeRequest);

        using HttpResponseMessage invokeResponse = await sender.SendAsync(invokeRequest, cancellationToken);
        string responseBody = await (invokeResponse.Content?.ReadAsStringAsync(cancellationToken) ?? Task.FromResult(string.Empty));

        return new LogicAppHttpInvokeResponse(
            resolvedWorkflowName,
            resolvedTriggerName,
            callbackUrl,
            LogicAppRoutes.ResolveRunId(invokeResponse),
            invokeResponse.StatusCode,
            responseBody,
            ReadHeaders(invokeResponse));
    }

    internal static LogicAppRunStatus ResolveCapturedStatus(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.RequestTimeout => LogicAppRunStatus.TimedOut,
            HttpStatusCode.GatewayTimeout => LogicAppRunStatus.TimedOut,
            _ when (int)statusCode is >= 200 and <= 299 => LogicAppRunStatus.Succeeded,
            _ => LogicAppRunStatus.Failed,
        };

    private static IReadOnlyDictionary<string, string[]> ReadHeaders(HttpResponseMessage response)
    {
        Dictionary<string, string[]> headers = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string key, IEnumerable<string> values) in response.Headers)
            headers[key] = values.ToArray();

        if (response.Content is not null)
        {
            foreach ((string key, IEnumerable<string> values) in response.Content.Headers)
                headers[key] = values.ToArray();
        }

        return headers.Count == 0 ? LogicAppCapturedResult.EmptyHeaders : new ReadOnlyDictionary<string, string[]>(headers);
    }
}

internal sealed record LogicAppHttpInvokeResponse(
    string WorkflowName,
    string TriggerName,
    string CallbackUrl,
    string? RunId,
    HttpStatusCode StatusCode,
    string ResponseBody,
    IReadOnlyDictionary<string, string[]> ResponseHeaders);