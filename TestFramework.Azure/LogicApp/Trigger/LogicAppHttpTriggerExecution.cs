using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Http;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Config;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.LogicApp.Trigger;

internal static class LogicAppHttpTriggerExecution
{
    /// <remarks>
    /// Takes the run rather than two pieces of one: a Logic App's configuration is read from the run now,
    /// so a service provider could not answer for it, and the variable store was only ever the run's.
    /// </remarks>
    internal static async Task<LogicAppHttpInvokeResponse> InvokeAsync(
        RunContext context,
        LogicAppIdentifier identifier,
        VariableReference<string>? workflowName,
        VariableReference<string> triggerName,
        VariableReference<CommonHttpRequest> request,
        bool includeResponseBody,
        CancellationToken cancellationToken)
    {
        VariableStore variableStore = context.Variables;
        IServiceProvider serviceProvider = context.Services;

        LogicAppConfig config = context.Configured<LogicAppConfig>(identifier);
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
        await HttpShaping.ApplyAsync(resolvedRequest, invokeRequest, cancellationToken);

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
            HttpShaping.ReadHeaders(invokeResponse));
    }

    internal static LogicAppRunStatus ResolveCapturedStatus(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.RequestTimeout => LogicAppRunStatus.TimedOut,
            HttpStatusCode.GatewayTimeout => LogicAppRunStatus.TimedOut,
            _ when (int)statusCode is >= 200 and <= 299 => LogicAppRunStatus.Succeeded,
            _ => LogicAppRunStatus.Failed,
        };

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
        HttpRequestMessage ownedInvokeRequest = await HttpShaping.CloneRequestAsync(invokeRequest, cancellationToken);
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

    private static HttpResponseMessage CreateObservedRunResponse(string callbackUrl, string runId)
    {
        HttpResponseMessage response = new(HttpStatusCode.Accepted);

        // RequestMessage is purely cosmetic on this synthesized response, and the callback URL carries
        // a SAS token, so only the redacted form is attached.
        string? redactedCallbackUrl = LogicAppUriRedaction.Redact(callbackUrl);
        if (redactedCallbackUrl is not null)
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Post, redactedCallbackUrl);

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