using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.FunctionApp.Trigger;

internal class HttpRemoteFunctionAppTrigger(FunctionAppIdentifier appIdentifier, VariableReference<TriggerHttpRouting> trigger, VariableReference<CommonHttpRequest> request) : Step<HttpResponseResultContext>, IHasEnvironmentRequirements
{
    public override string Name => "Http Remote FunctionApp Trigger";

    public override string Description => "A Trigger for Triggering a FunctionApp-Http-Trigger";

    public override bool DoesReturn => true;

    public override Step<HttpResponseResultContext> Clone()
    {
        return new HttpRemoteFunctionAppTrigger(appIdentifier, trigger, request).WithClonedOptions(this);
    }

    public override async Task<HttpResponseResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        var _trigger = trigger.GetRequiredValue(variableStore);
        var _request = request.GetRequiredValue(variableStore);

        FunctionAppTriggerConfig config = serviceProvider.GetService<FunctionAppTriggerConfig>() ?? new FunctionAppTriggerConfig();
        FunctionAppConfig functionConfig = serviceProvider.GetRequiredService<ConfigStore<FunctionAppConfig>>().GetConfig(appIdentifier);
        RemoteConnection remoteConnection = new RemoteConnection(new Uri(functionConfig.BaseUrl));
        if (config.DoPing) await remoteConnection.EnsurePingAsync();
        Uri fullTriggerUri = BuildTriggerUri(remoteConnection.BasePath, functionConfig.RoutePrefix, _trigger.Path);
        IHttpRequestSender sender = serviceProvider.GetAzureComponentFactory().Http.CreateSender();
        logger.LogInformation("FunctionApp HTTP '{0}' -> {1} {2}", appIdentifier, _trigger.Method, fullTriggerUri);

        using HttpResponseMessage response = await SendWithLocalWarmupRetryAsync(
            sender,
            fullTriggerUri,
            _trigger.Method,
            _request,
            functionConfig,
            config,
            logger,
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation("FunctionApp HTTP '{0}' <- {1}", appIdentifier, response.StatusCode);

        return await HttpResponseResultContext.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> SendWithLocalWarmupRetryAsync(
        IHttpRequestSender sender,
        Uri fullTriggerUri,
        HttpMethod method,
        CommonHttpRequest request,
        FunctionAppConfig functionConfig,
        FunctionAppTriggerConfig triggerConfig,
        ScopedLogger logger,
        CancellationToken cancellationToken)
    {
        // This is intentionally narrower than timeline retries: local hosts often return a transient 404
        // while the route table is still warming up, and callers should not need to model that startup quirk
        // as a user-visible step retry.
        bool shouldRetryNotFound = IsLocalDevelopmentHost(fullTriggerUri) && triggerConfig.LocalNotFoundRetryDuration > TimeSpan.Zero;
        DateTime retryDeadline = DateTime.UtcNow.Add(triggerConfig.LocalNotFoundRetryDuration);

        while (true)
        {
            using HttpRequestMessage message = await CreateRequestMessageAsync(fullTriggerUri, method, request, functionConfig, cancellationToken).ConfigureAwait(false);
            HttpResponseMessage response = await sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!shouldRetryNotFound || response.StatusCode != HttpStatusCode.NotFound || DateTime.UtcNow >= retryDeadline)
                return response;

            logger.LogWarning("FunctionApp HTTP warmup retry for '{0}' after 404 from local host. Retrying after {1}.", fullTriggerUri, triggerConfig.LocalNotFoundRetryDelay);
            response.Dispose();
            await Task.Delay(triggerConfig.LocalNotFoundRetryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<HttpRequestMessage> CreateRequestMessageAsync(Uri fullTriggerUri, HttpMethod method, CommonHttpRequest request, FunctionAppConfig functionConfig, CancellationToken cancellationToken)
    {
        HttpRequestMessage message = new(method, fullTriggerUri);
        await request.ApplyToHttpRequestMessageAsync(message, cancellationToken).ConfigureAwait(false);
        message.Headers.Add("x-functions-key", functionConfig.Code);
        return message;
    }

    /// <summary>
    /// Combines the site root, the host route prefix and the route the builder produced.
    /// </summary>
    /// <remarks>
    /// This is the one place the <c>api/</c> prefix is applied. Routes arriving here are always
    /// relative to the prefix, whether they came from a custom <c>[HttpTrigger(Route = ...)]</c>,
    /// a bare function name or an explicit sub-path.
    /// </remarks>
    internal static Uri BuildTriggerUri(Uri siteRoot, string? routePrefix, string relativePath)
    {
        Uri root = siteRoot.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? siteRoot
            : new Uri(siteRoot.AbsoluteUri + "/");

        string path = (relativePath ?? string.Empty).TrimStart('/');
        string prefix = (routePrefix ?? string.Empty).Trim('/');

        return new Uri(root, prefix.Length == 0 ? path : $"{prefix}/{path}");
    }

    private static bool IsLocalDevelopmentHost(Uri uri)
        => LocalEndpoints.IsLocalDevelopmentHost(uri);

    public override StepInstance<Step<HttpResponseResultContext>, HttpResponseResultContext> GetInstance() =>
        new StepInstance<Step<HttpResponseResultContext>, HttpResponseResultContext>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (trigger.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(trigger.Identifier!.Identifier, StepIOKind.Variable, true, typeof(TriggerHttpRouting)));
        if (request.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(request.Identifier!.Identifier, StepIOKind.Variable, true, typeof(CommonHttpRequest)));
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.FunctionApp, appIdentifier)];
}