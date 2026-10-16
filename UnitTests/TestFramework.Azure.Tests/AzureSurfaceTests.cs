using TestFramework.Core.Steps.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Azure.ServiceBus;
using TestFramework.Core.Environment;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Tests;

public class AzureSurfaceTests
{
    [Fact]
    public void GetAzureComponentFactory_ReturnsDefaultFactory_WhenNoOverrideIsRegistered()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

        IAzureComponentFactory first = provider.GetAzureComponentFactory();
        IAzureComponentFactory second = provider.GetAzureComponentFactory();

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void GetAzureComponentFactory_ReturnsRegisteredOverride_WhenProvided()
    {
        StubAzureComponentFactory factory = new();
        ServiceCollection services = new();
        services.AddSingleton<IAzureComponentFactory>(factory);
        using ServiceProvider provider = services.BuildServiceProvider();

        IAzureComponentFactory resolved = provider.GetAzureComponentFactory();

        Assert.Same(factory, resolved);
    }

    [Fact]
    public void AzureEnv_ServiceBusSend_ReturnsConcreteTrigger()
    {
        ServiceBusSendTrigger trigger = AzureExt.Trigger.ServiceBus.Send("bus", Var.Const(new ServiceBusMessage("payload")));

        Assert.Equal("ServiceBus Send Trigger", trigger.Name);
    }

    [Fact]
    public void AzureEnv_MessageReceived_WithTempSubscriptionFlag_ProvidesPreAndCleanupSteps()
    {
        ServiceBusProcessEvent step = AzureExt.Event.ServiceBus.MessageReceived("bus").WithTempSubscription();

        StepGeneric? preStep = ((IHasPreStep)step).CreatePreStep(null!);
        StepGeneric? cleanupStep = ((IHasCleanupStep)step).CreateCleanupStep(null!);

        Assert.IsType<ServiceBusCreateTempSubscriptionStep>(preStep);
        Assert.IsType<ServiceBusDeleteTempSubscriptionStep>(cleanupStep);
        Assert.Equal("bus", Assert.Single(((IHasEnvironmentRequirements)step).GetEnvironmentRequirements(null!)).ResourceIdentifier);
    }

    [Fact]
    public void AzureEnv_IsLive_ReturnsStepsForKnownTargets()
    {
        var logicApp = AzureExt.Trigger.IsLive.LogicApp("logic", AlivenessLevel.Authenticated);
        var functionApp = AzureExt.Trigger.IsLive.FunctionApp("func");
        var serviceBus = AzureExt.Trigger.IsLive.ServiceBus("bus", AlivenessLevel.Authenticated);
        var blob = AzureExt.Trigger.IsLive.Blob("storage", AlivenessLevel.Authenticated);
        var table = AzureExt.Trigger.IsLive.Table("storage", AlivenessLevel.Authenticated);
        var cosmos = AzureExt.Trigger.IsLive.CosmosDb("cosmos", AlivenessLevel.Authenticated);
        var sql = AzureExt.Trigger.IsLive.SqlDatabase("sql", AlivenessLevel.Authenticated);

        Assert.Equal("LogicApp IsLive Trigger", logicApp.Name);
        Assert.Equal("FunctionApp IsLive Trigger", functionApp.Name);
        Assert.Equal("ServiceBus IsLive Trigger", serviceBus.Name);
        Assert.Equal("StorageAccount Blob IsLive Trigger", blob.Name);
        Assert.Equal("StorageAccount Table IsLive Trigger", table.Name);
        Assert.Equal("CosmosDb IsLive Trigger", cosmos.Name);
        Assert.Equal("SqlDatabase IsLive Trigger", sql.Name);
        Assert.Equal("logic", Assert.Single(((IHasEnvironmentRequirements)logicApp).GetEnvironmentRequirements(null!)).ResourceIdentifier);
        Assert.Equal("bus", Assert.Single(((IHasEnvironmentRequirements)serviceBus).GetEnvironmentRequirements(null!)).ResourceIdentifier);
        Assert.Equal("storage", Assert.Single(((IHasEnvironmentRequirements)blob).GetEnvironmentRequirements(null!)).ResourceIdentifier);
        Assert.Equal("storage", Assert.Single(((IHasEnvironmentRequirements)table).GetEnvironmentRequirements(null!)).ResourceIdentifier);
        Assert.Equal("cosmos", Assert.Single(((IHasEnvironmentRequirements)cosmos).GetEnvironmentRequirements(null!)).ResourceIdentifier);
        Assert.Equal("sql", Assert.Single(((IHasEnvironmentRequirements)sql).GetEnvironmentRequirements(null!)).ResourceIdentifier);
    }

    [Fact]
    public void AzureEnv_ServiceBusSend_ProvidesEnvironmentRequirement()
    {
        ServiceBusSendTrigger trigger = AzureExt.Trigger.ServiceBus.Send("bus", Var.Const(new ServiceBusMessage("payload")));

        EnvironmentRequirement requirement = Assert.Single(((IHasEnvironmentRequirements)trigger).GetEnvironmentRequirements(null!));

        Assert.Equal(AzureResourceKinds.ServiceBus, requirement.ResourceKind);
        Assert.Equal("bus", requirement.ResourceIdentifier);
    }

    [Fact]
    public void AzureEnvironmentResourceKinds_ExposesLogicAppKind()
    {
        LogicAppIdentifier identifier = "logic";

        Assert.Equal("logic", identifier.ToString());
        Assert.Equal("azure.logicapp", AzureResourceKinds.LogicApp);
    }

    [Fact]
    public void NarrowingAServiceBusWait_ReturnsANewEventAndLeavesTheOriginalAsItWas()
    {
        // An event handed to one timeline and narrowed again for another must not change under the first.
        ServiceBusProcessEvent any = AzureExt.Event.ServiceBus.MessageReceived("bus");
        ServiceBusProcessEvent narrowed = any.WithMessageId(Var.Ref<string>("messageId")).WithCorrelationId(Var.Ref<string>("correlationId"));

        StepIOContract anyContract = new();
        any.DeclareIO(anyContract);
        StepIOContract narrowedContract = new();
        narrowed.DeclareIO(narrowedContract);

        Assert.NotSame(any, narrowed);
        Assert.Empty(anyContract.Inputs);
        Assert.Equal(["correlationId", "messageId"], narrowedContract.Inputs.Select(input => input.Key).Order());
    }

    [Fact]
    public void AzureIsLiveResult_ExposesKnownResourceKind()
    {
        Azure.Trigger.IsLive.AzureIsLiveResult result = new(
            AzureResourceKinds.FunctionApp,
            "func",
            AlivenessLevel.Authenticated,
            null);

        Assert.Equal(Azure.Trigger.IsLive.AzureKnownResourceKind.FunctionApp, result.KnownResourceKind);
    }

    [Fact]
    public void LogicAppResults_ExposeInvokeUriAliasOverCallbackUrl()
    {
        Azure.LogicApp.LogicAppTriggerResult triggerResult = new("workflow", "manual", "https://callback", "run-1", System.Net.HttpStatusCode.Accepted);
        Azure.LogicApp.LogicAppCapturedResult capturedResult = new("workflow", "manual", "https://callback", "run-1", System.Net.HttpStatusCode.OK, Azure.LogicApp.LogicAppRunStatus.Succeeded, "{}", Azure.LogicApp.LogicAppCapturedResult.EmptyHeaders);

        Assert.Equal(triggerResult.CallbackUrl, triggerResult.InvokeUri);
        Assert.Equal(capturedResult.CallbackUrl, capturedResult.InvokeUri);
    }

    private sealed class StubAzureComponentFactory : IAzureComponentFactory
    {
        public ICosmosComponentFactory Cosmos { get; } = new StubCosmosFactory();
        public IBlobComponentFactory Blob { get; } = new StubBlobFactory();
        public ITableComponentFactory Table { get; } = new StubTableFactory();
        public IServiceBusComponentFactory ServiceBus { get; } = new StubServiceBusFactory();
        public IHttpComponentFactory Http { get; } = new StubHttpFactory();
    }

    private sealed class StubCosmosFactory : ICosmosComponentFactory
    {
        public ICosmosContainerAdapter CreateContainer(Resolved<Configuration.SpecificConfigs.CosmosDbConfig> cosmos) => throw new NotSupportedException();
    }

    private sealed class StubBlobFactory : IBlobComponentFactory
    {
        public IBlobContainerAdapter CreateContainer(Resolved<Configuration.SpecificConfigs.StorageAccountConfig> account) => throw new NotSupportedException();
    }

    private sealed class StubTableFactory : ITableComponentFactory
    {
        public ITableAdapter CreateTable(Resolved<Configuration.SpecificConfigs.StorageAccountConfig> account, string tableName) => throw new NotSupportedException();
    }

    private sealed class StubServiceBusFactory : IServiceBusComponentFactory
    {
        public IServiceBusSenderAdapter CreateSender(Resolved<Configuration.SpecificConfigs.ServiceBusConfig> serviceBus) => throw new NotSupportedException();
        public IServiceBusMessagePump CreateMessagePump(Resolved<Configuration.SpecificConfigs.ServiceBusConfig> serviceBus, string? subscriptionName) => throw new NotSupportedException();
        public IServiceBusAdministrationAdapter CreateAdministration(Resolved<Configuration.SpecificConfigs.ServiceBusConfig> serviceBus) => throw new NotSupportedException();
    }

    private sealed class StubHttpFactory : IHttpComponentFactory
    {
        public IHttpRequestSender CreateSender() => throw new NotSupportedException();
    }
}