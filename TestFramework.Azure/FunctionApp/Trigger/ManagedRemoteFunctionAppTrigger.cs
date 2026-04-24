using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Net.Mime;
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

internal class ManagedRemoteFunctionAppTrigger(FunctionAppIdentifier appIdentifier, VariableReference<TriggerRouting> trigger) : Step<ManagedResult>, IHasEnvironmentRequirements
{
    private static readonly Uri AdminFunctionsUri = new Uri("admin/functions", UriKind.Relative);

    public override string Name => "Managed Remote FunctionApp Trigger";

    public override string Description => "A Trigger for Triggering a Managed-FunctionApp-Trigger";

    public override bool DoesReturn => true;

    public override Step<ManagedResult> Clone()
    {
        return new ManagedRemoteFunctionAppTrigger(appIdentifier, trigger).WithClonedOptions(this);
    }

    public override async Task<ManagedResult?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        var _trigger = trigger.GetRequiredValue(variableStore);

        FunctionAppTriggerConfig config = serviceProvider.GetService<FunctionAppTriggerConfig>() ?? new FunctionAppTriggerConfig();
        FunctionAppConfig functionConfig = serviceProvider.GetRequiredService<ConfigStore<FunctionAppConfig>>().GetConfig(appIdentifier);
        RemoteConnection remoteConnection = new RemoteConnection(new Uri(functionConfig.BaseUrl));
        if (config.DoPing) await remoteConnection.EnsurePingAsync();
        Uri fullTriggerUri = new Uri(remoteConnection.BasePath, $"{AdminFunctionsUri}/{_trigger.Name}");
        HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, fullTriggerUri);
        message.Headers.Add("x-functions-key", functionConfig.Code);
        message.Content = new StringContent("{}", new System.Net.Http.Headers.MediaTypeHeaderValue(MediaTypeNames.Application.Json));
        IHttpRequestSender sender = serviceProvider.GetAzureComponentFactory().Http.CreateSender();
        using HttpResponseMessage response = await sender.SendAsync(message, cancellationToken);
        string? responseBody = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return new ManagedResult()
        {
            StatusCode = response.StatusCode,
            Body = responseBody,
        };
    }

    public override StepInstance<Step<ManagedResult>, ManagedResult> GetInstance() =>
        new StepInstance<Step<ManagedResult>, ManagedResult>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (trigger.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(trigger.Identifier!.Identifier, StepIOKind.Variable, true, typeof(TriggerRouting)));
    }

    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [new EnvironmentRequirement(AzureEnvironmentResourceKinds.FunctionApp, appIdentifier)];
}