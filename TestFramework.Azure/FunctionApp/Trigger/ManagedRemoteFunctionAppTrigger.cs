using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
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

    public override async Task<ManagedResult?> Execute(RunContext context)
    {
        var _trigger = trigger.GetRequiredValue(context.Variables);

        FunctionAppTriggerConfig config = context.Services.GetService<FunctionAppTriggerConfig>() ?? new FunctionAppTriggerConfig();
        FunctionAppConfig functionConfig = context.Configured<FunctionAppConfig>(appIdentifier);
        RemoteConnection remoteConnection = new RemoteConnection(new Uri(functionConfig.BaseUrl));
        if (config.DoPing) await remoteConnection.EnsurePingAsync();
        Uri fullTriggerUri = new Uri(remoteConnection.BasePath, $"{AdminFunctionsUri}/{_trigger.Name}");
        using HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, fullTriggerUri);

        // The admin endpoint authenticates with the MASTER key, not a function key. Fall back to Code
        // only because a locally hosted host accepts anything.
        message.Headers.Add("x-functions-key", functionConfig.AdminCode ?? functionConfig.Code);
        message.Content = new StringContent("{}", new System.Net.Http.Headers.MediaTypeHeaderValue(MediaTypeNames.Application.Json));
        IHttpRequestSender sender = context.Services.GetAzureComponentFactory().Http.CreateSender();
        context.Logger.LogInformation("FunctionApp managed '{0}' -> POST {1} (function: {2})", appIdentifier, fullTriggerUri, _trigger.Name);
        using HttpResponseMessage response = await sender.SendAsync(message, context.Deadline.Token);
        string? responseBody = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync(context.Deadline.Token);
        context.Logger.LogInformation("FunctionApp managed '{0}' <- {1}", appIdentifier, response.StatusCode);

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"The Function App admin endpoint '{fullTriggerUri}' rejected the key with " +
                $"{(int)response.StatusCode} ({response.StatusCode}). This endpoint requires the host master key: " +
                $"set 'AdminCode' on the '{appIdentifier}' Function App configuration.");
        }

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