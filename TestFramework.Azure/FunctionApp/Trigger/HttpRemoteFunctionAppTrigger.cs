using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
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

internal class HttpRemoteFunctionAppTrigger(FunctionAppIdentifier appIdentifier, VariableReference<TriggerHttpRouting> trigger, VariableReference<CommonHttpRequest> request) : Step<HttpResponseMessage>, IHasEnvironmentRequirements
{
    public override string Name => "Http Remote FunctionApp Trigger";

    public override string Description => "A Trigger for Triggering a FunctionApp-Http-Trigger";

    public override bool DoesReturn => true;

    public override Step<HttpResponseMessage> Clone()
    {
        return new HttpRemoteFunctionAppTrigger(appIdentifier, trigger, request).WithClonedOptions(this);
    }

    public override async Task<HttpResponseMessage?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        var _trigger = trigger.GetRequiredValue(variableStore);
        var _request = request.GetRequiredValue(variableStore);

        FunctionAppTriggerConfig config = serviceProvider.GetService<FunctionAppTriggerConfig>() ?? new FunctionAppTriggerConfig();
        FunctionAppConfig functionConfig = serviceProvider.GetRequiredService<ConfigStore<FunctionAppConfig>>().GetConfig(appIdentifier);
        RemoteConnection remoteConnection = new RemoteConnection(new Uri(functionConfig.BaseUrl));
        if (config.DoPing) await remoteConnection.EnsurePingAsync();
        Uri fullTriggerUri = new Uri(remoteConnection.BasePath, _trigger.Path);
        IHttpRequestSender sender = serviceProvider.GetAzureComponentFactory().Http.CreateSender();

        return await SendWithLocalWarmupRetryAsync(
            sender,
            fullTriggerUri,
            _trigger.Method,
            _request,
            functionConfig,
            config,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> SendWithLocalWarmupRetryAsync(
        IHttpRequestSender sender,
        Uri fullTriggerUri,
        HttpMethod method,
        CommonHttpRequest request,
        FunctionAppConfig functionConfig,
        FunctionAppTriggerConfig triggerConfig,
        CancellationToken cancellationToken)
    {
        bool shouldRetryNotFound = IsLocalDevelopmentHost(fullTriggerUri) && triggerConfig.LocalNotFoundRetryDuration > TimeSpan.Zero;
        DateTime retryDeadline = DateTime.UtcNow.Add(triggerConfig.LocalNotFoundRetryDuration);

        while (true)
        {
            using HttpRequestMessage message = CreateRequestMessage(fullTriggerUri, method, request, functionConfig);
            HttpResponseMessage response = await sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!shouldRetryNotFound || response.StatusCode != HttpStatusCode.NotFound || DateTime.UtcNow >= retryDeadline)
                return response;

            response.Dispose();
            await Task.Delay(triggerConfig.LocalNotFoundRetryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static HttpRequestMessage CreateRequestMessage(Uri fullTriggerUri, HttpMethod method, CommonHttpRequest request, FunctionAppConfig functionConfig)
    {
        HttpRequestMessage message = new(method, fullTriggerUri);
        request.ApplyToHttpRequestMessage(message);
        message.Headers.Add("x-functions-key", functionConfig.Code);
        return message;
    }

    private static bool IsLocalDevelopmentHost(Uri uri)
        => uri.IsLoopback
        || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Host, "host.docker.internal", StringComparison.OrdinalIgnoreCase);

    public override StepInstance<Step<HttpResponseMessage>, HttpResponseMessage> GetInstance() =>
        new StepInstance<Step<HttpResponseMessage>, HttpResponseMessage>(this);

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