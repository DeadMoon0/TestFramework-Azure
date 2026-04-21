using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure;
using TestFramework.Azure.Runtime;
using TestFramework.Azure.ServiceBus;
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
    public void AzureTF_ServiceBusSend_ReturnsConcreteTrigger()
    {
        ServiceBusSendTrigger trigger = AzureTF.Trigger.ServiceBus.Send("bus", Var.Const(new ServiceBusMessage("payload")));

        Assert.Equal("ServiceBus Send Trigger", trigger.Name);
    }

    [Fact]
    public void AzureTF_MessageReceived_WithTempSubscriptionFlag_ProvidesPreAndCleanupSteps()
    {
        ServiceBusProcessEvent step = AzureTF.Event.ServiceBus.MessageReceived("bus", createTempSubscription: true);

        StepGeneric? preStep = ((IHasPreStep)step).CreatePreStep(null!);
        StepGeneric? cleanupStep = ((IHasCleanupStep)step).CreateCleanupStep(null!);

        Assert.IsType<ServiceBusCreateTempSubscriptionStep>(preStep);
        Assert.IsType<ServiceBusDeleteTempSubscriptionStep>(cleanupStep);
    }

    [Fact]
    public void AzureTF_IsLive_ReturnsStepsForKnownTargets()
    {
        Step<object?> functionApp = AzureTF.Trigger.IsLive.FunctionApp("func");
        Step<object?> serviceBus = AzureTF.Trigger.IsLive.ServiceBus("bus");
        Step<object?> blob = AzureTF.Trigger.IsLive.Blob("storage");
        Step<object?> table = AzureTF.Trigger.IsLive.Table("storage");
        Step<object?> cosmos = AzureTF.Trigger.IsLive.Cosmos("cosmos");
        Step<object?> sql = AzureTF.Trigger.IsLive.Sql("sql");

        Assert.Equal("FunctionApp IsLive Trigger", functionApp.Name);
        Assert.Equal("ServiceBus IsLive Trigger", serviceBus.Name);
        Assert.Equal("Blob Storage IsLive Trigger", blob.Name);
        Assert.Equal("Table Storage IsLive Trigger", table.Name);
        Assert.Equal("Cosmos Container IsLive Trigger", cosmos.Name);
        Assert.Equal("SqlDatabase IsLive Trigger", sql.Name);
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
        public ICosmosContainerAdapter CreateContainer(Configuration.SpecificConfigs.CosmosContainerDbConfig config) => throw new NotSupportedException();
    }

    private sealed class StubBlobFactory : IBlobComponentFactory
    {
        public IBlobContainerAdapter CreateContainer(Configuration.SpecificConfigs.StorageAccountConfig config) => throw new NotSupportedException();
    }

    private sealed class StubTableFactory : ITableComponentFactory
    {
        public ITableAdapter CreateTable(Configuration.SpecificConfigs.StorageAccountConfig config, string tableName) => throw new NotSupportedException();
    }

    private sealed class StubServiceBusFactory : IServiceBusComponentFactory
    {
        public IServiceBusSenderAdapter CreateSender(Configuration.SpecificConfigs.ServiceBusConfig config) => throw new NotSupportedException();
        public IServiceBusMessagePump CreateMessagePump(Configuration.SpecificConfigs.ServiceBusConfig config, string? subscriptionName) => throw new NotSupportedException();
        public IServiceBusAdministrationAdapter CreateAdministration(Configuration.SpecificConfigs.ServiceBusConfig config) => throw new NotSupportedException();
    }

    private sealed class StubHttpFactory : IHttpComponentFactory
    {
        public IHttpRequestSender CreateSender() => throw new NotSupportedException();
    }
}