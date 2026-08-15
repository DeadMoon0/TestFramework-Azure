using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
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
        bool includeResponseBody,
        CancellationToken cancellationToken)
    {
        LogicAppConfig config = serviceProvider.GetRequiredService<ConfigStore<LogicAppConfig>>().GetConfig(identifier);
        string resolvedWorkflowName = LogicAppRoutes.ResolveWorkflowName(config, workflowName?.GetValue(variableStore));
        string resolvedTriggerName = triggerName.GetRequiredValue(variableStore);
        config.EnsureSupportsDirectInvocation("invoke Logic App HTTP triggers");
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
            string callbackUrlPayload = await ReadCallbackUrlPayloadAsync(callbackUrlResponse, cancellationToken);
            callbackUrl = LogicAppJson.RebaseLocalCallbackUrl(LogicAppRoutes.RequireStandardBaseUrl(config), LogicAppJson.ExtractCallbackUrl(callbackUrlPayload));
        }

        using HttpRequestMessage invokeRequest = new(HttpMethod.Post, callbackUrl);
        await resolvedRequest.ApplyToHttpRequestMessageAsync(invokeRequest, cancellationToken);

        DateTimeOffset observationStart = DateTimeOffset.UtcNow;
        using HttpResponseMessage invokeResponse = includeResponseBody || !config.SupportsManagementOperations
            ? await sender.SendAsync(invokeRequest, cancellationToken)
            : await SendForRunTrackingAsync(serviceProvider, sender, identifier, config, resolvedWorkflowName, resolvedTriggerName, callbackUrl, invokeRequest, observationStart, cancellationToken);
        string responseBody = includeResponseBody
            ? await (invokeResponse.Content?.ReadAsStringAsync(cancellationToken) ?? Task.FromResult(string.Empty))
            : string.Empty;

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

    private static async Task<string> ReadCallbackUrlPayloadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
            throw new TestFramework.Core.Exceptions.FrameworkStateException("The Logic App callback URL response did not contain a body.");

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        byte[] buffer = new byte[4096];
        StringBuilder payloadBuilder = new();

        while (true)
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead == 0)
                break;

            payloadBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            string currentPayload = payloadBuilder.ToString();
            if (LogicAppJson.TryExtractCallbackUrl(currentPayload, out _))
                return currentPayload;
        }

        string payload = payloadBuilder.ToString();
        if (!string.IsNullOrWhiteSpace(payload))
            return payload;

        throw new TestFramework.Core.Exceptions.FrameworkStateException("The Logic App callback URL response did not contain a readable payload.");
    }

    private static async Task<HttpResponseMessage> SendForRunTrackingAsync(
        IServiceProvider serviceProvider,
        IHttpRequestSender sender,
        LogicAppIdentifier identifier,
        LogicAppConfig config,
        string workflowName,
        string triggerName,
        string callbackUrl,
        HttpRequestMessage invokeRequest,
        DateTimeOffset observationStart,
        CancellationToken cancellationToken)
    {
        HttpRequestMessage ownedInvokeRequest = await CloneHttpRequestMessageAsync(invokeRequest, cancellationToken);
        CancellationTokenSource invokeLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationTokenSource observationLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<HttpResponseMessage> invokeTask = sender.SendAsync(ownedInvokeRequest, invokeLifetime.Token);
        Task<string> observedRunTask = LogicAppManagementTrigger.WaitForObservedRunAsync(serviceProvider, sender, identifier, config, workflowName, triggerName, observationStart, observationLifetime.Token);

        try
        {
            Task completedTask = await Task.WhenAny(invokeTask, observedRunTask);
            if (completedTask == invokeTask)
            {
                HttpResponseMessage response = await invokeTask;
                ownedInvokeRequest.Dispose();

                if (!string.IsNullOrWhiteSpace(LogicAppRoutes.ResolveRunId(response)))
                {
                    observationLifetime.Cancel();
                    _ = ObserveCompletionAsync(observedRunTask, observationLifetime);
                    invokeLifetime.Dispose();
                    return response;
                }

                response.Dispose();
                string observedRunIdFromCompletedInvoke = await observedRunTask;
                observationLifetime.Dispose();
                invokeLifetime.Dispose();
                return CreateObservedRunResponse(callbackUrl, observedRunIdFromCompletedInvoke);
            }

            string observedRunId = await observedRunTask;
            invokeLifetime.Cancel();
            _ = ObserveCompletionAsync(invokeTask, ownedInvokeRequest, invokeLifetime);
            observationLifetime.Dispose();
            return CreateObservedRunResponse(callbackUrl, observedRunId);
        }
        catch
        {
            CancelIfPending(invokeTask, invokeLifetime);
            CancelIfPending(observedRunTask, observationLifetime);
            ownedInvokeRequest.Dispose();
            invokeLifetime.Dispose();
            observationLifetime.Dispose();
            throw;
        }
    }

    private static void CancelIfPending(Task task, CancellationTokenSource cancellationTokenSource)
    {
        if (task.IsCompleted)
            return;

        cancellationTokenSource.Cancel();
    }

    private static async Task ObserveCompletionAsync(Task task, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }

    private static async Task ObserveCompletionAsync(Task<HttpResponseMessage> task, HttpRequestMessage invokeRequest, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            HttpResponseMessage response = await task;
            response.Dispose();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            invokeRequest.Dispose();
            cancellationTokenSource.Dispose();
        }
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage source, CancellationToken cancellationToken)
    {
        HttpRequestMessage clone = new(source.Method, source.RequestUri);

        foreach ((string key, IEnumerable<string> values) in source.Headers)
            clone.Headers.TryAddWithoutValidation(key, values);

        if (source.Content is not null)
        {
            byte[] contentBytes = await source.Content.ReadAsByteArrayAsync(cancellationToken);
            ByteArrayContent clonedContent = new(contentBytes);

            foreach ((string key, IEnumerable<string> values) in source.Content.Headers)
                clonedContent.Headers.TryAddWithoutValidation(key, values);

            clone.Content = clonedContent;
        }

        return clone;
    }

    private static HttpResponseMessage CreateObservedRunResponse(string callbackUrl, string runId)
    {
        HttpResponseMessage response = new(HttpStatusCode.Accepted);
        response.RequestMessage = new HttpRequestMessage(HttpMethod.Post, callbackUrl);
        response.Headers.Add("x-ms-workflow-run-id", runId);
        return response;
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