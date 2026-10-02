using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.Extensions;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Azure.Tests;

/// <summary>
/// Azure's configured resources as the run's resource graph sees them.
/// </summary>
/// <remarks>
/// <para>
/// Six sections, six kinds, one question. A step that needs a Service Bus connection string or a Cosmos
/// container name asks the run rather than resolving somebody's configuration record, and the answer is the
/// same shape whether a person wrote the value down or something published it while starting.
/// </para>
/// <para>
/// Each section is read by the package's own provider, so the store the steps use and the graph the run
/// exposes cannot come to different conclusions about what the file said.
/// </para>
/// </remarks>
public class AzureResourceGraphTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AFunctionAppPublishesItsAddressAndRoutePrefix()
    {
        Assert.Equal(
            "http://localhost:7071/",
            await Read(
                AzureEnvironmentResourceKinds.FunctionApp,
                "orders-fn",
                ValueNames.BaseUrl,
                ("FunctionApp:orders-fn:BaseUrl", "http://localhost:7071/"),
                ("FunctionApp:orders-fn:Code", "secret-key")));

        Assert.Equal(
            "api",
            await Read(
                AzureEnvironmentResourceKinds.FunctionApp,
                "orders-fn",
                AzureEnvironmentResourceKinds.RoutePrefixValue,
                ("FunctionApp:orders-fn:BaseUrl", "http://localhost:7071/"),
                ("FunctionApp:orders-fn:Code", "secret-key")));
    }

    [Fact]
    public async Task AFunctionKeyTravelsAndIsNeverPrinted()
    {
        // **This reverses a rule that used to be asserted here**, and the reversal is the point. A credential
        // used to stay out of the graph and live in a typed configuration store the steps read instead - which
        // worked only for as long as there was a store. There is not one now: a run's configuration is run
        // data and travels on the run, so a key that could not travel would be a key nothing could reach.
        //
        // What replaced the rule is stronger than it was. The key travels, and the kind declares it secret, so
        // the listing that failure messages and the frozen run are built from redacts it by construction
        // rather than by whoever formats them remembering to.
        (string Key, string Value)[] configured =
        [
            ("FunctionApp:orders-fn:BaseUrl", "http://localhost:7071/"),
            ("FunctionApp:orders-fn:Code", "secret-key"),
        ];

        // Reachable, because something has to authenticate with it.
        Assert.Equal(
            "secret-key",
            await Read(AzureEnvironmentResourceKinds.FunctionApp, "orders-fn", AzureEnvironmentResourceKinds.CodeValue, configured));

        // And absent from every listing.
        string listed = await Listing(configured);

        Assert.DoesNotContain("secret-key", listed, StringComparison.Ordinal);
        Assert.Contains("Code=(secret)", listed, StringComparison.Ordinal);

        // Not a blanket: the address beside it is not a credential and stays readable.
        Assert.Contains("http://localhost:7071/", listed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AServiceBusPublishesItsConnectionAndTheEntitiesItNames()
    {
        (string Key, string Value)[] configured =
        [
            ("ServiceBus:orders-bus:ConnectionString", "Endpoint=sb://localhost;SharedAccessKeyName=x;SharedAccessKey=y"),
            ("ServiceBus:orders-bus:TopicName", "orders"),
            ("ServiceBus:orders-bus:SubscriptionName", "orders-worker"),
        ];

        Assert.Equal(
            "Endpoint=sb://localhost;SharedAccessKeyName=x;SharedAccessKey=y",
            await Read(AzureEnvironmentResourceKinds.ServiceBus, "orders-bus", ValueNames.ConnectionString, configured));

        Assert.Equal(
            "orders",
            await Read(AzureEnvironmentResourceKinds.ServiceBus, "orders-bus", AzureEnvironmentResourceKinds.TopicNameValue, configured));

        Assert.Equal(
            "orders-worker",
            await Read(AzureEnvironmentResourceKinds.ServiceBus, "orders-bus", AzureEnvironmentResourceKinds.SubscriptionNameValue, configured));
    }

    [Fact]
    public async Task AStorageAccountPublishesTheContainersItNames()
    {
        (string Key, string Value)[] configured =
        [
            ("StorageAccount:files:ConnectionString", "UseDevelopmentStorage=true"),
            ("StorageAccount:files:BlobContainerName", "documents"),
        ];

        Assert.Equal(
            "UseDevelopmentStorage=true",
            await Read(AzureEnvironmentResourceKinds.Storage, "files", ValueNames.ConnectionString, configured));

        Assert.Equal(
            "documents",
            await Read(AzureEnvironmentResourceKinds.Storage, "files", AzureEnvironmentResourceKinds.BlobContainerNameValue, configured));
    }

    [Fact]
    public async Task ACosmosContainerPublishesAllThreeThingsNeededToReachAnItem()
    {
        (string Key, string Value)[] configured =
        [
            ("CosmosDb:orders:ConnectionString", "AccountEndpoint=https://localhost:8081/;AccountKey=key"),
            ("CosmosDb:orders:DatabaseName", "shop"),
            ("CosmosDb:orders:ContainerName", "orders"),
        ];

        Assert.Equal("shop", await Read(AzureEnvironmentResourceKinds.Cosmos, "orders", AzureEnvironmentResourceKinds.DatabaseNameValue, configured));
        Assert.Equal("orders", await Read(AzureEnvironmentResourceKinds.Cosmos, "orders", AzureEnvironmentResourceKinds.ContainerNameValue, configured));
    }

    [Fact]
    public async Task ASqlDatabasePublishesItsConnectionAndName()
    {
        (string Key, string Value)[] configured =
        [
            ("SqlDatabase:orders-db:ConnectionString", "Server=localhost;Database=orders;Integrated Security=true"),
            ("SqlDatabase:orders-db:DatabaseName", "orders"),
        ];

        Assert.Equal(
            "Server=localhost;Database=orders;Integrated Security=true",
            await Read(AzureEnvironmentResourceKinds.Sql, "orders-db", ValueNames.ConnectionString, configured));

        Assert.Equal("orders", await Read(AzureEnvironmentResourceKinds.Sql, "orders-db", AzureEnvironmentResourceKinds.DatabaseNameValue, configured));
    }

    [Fact]
    public async Task AStandardLogicAppPublishesItsHostAddress()
        => Assert.Equal(
            "https://orders.azurewebsites.net/",
            await Read(
                AzureEnvironmentResourceKinds.LogicApp,
                "orders-flow",
                ValueNames.BaseUrl,
                ("LogicApp:orders-flow:HostingMode", "Standard"),
                ("LogicApp:orders-flow:Standard:BaseUrl", "https://orders.azurewebsites.net/")));

    [Fact]
    public async Task AConsumptionLogicAppPublishesItsInvokeUrlUnderTheSameName()
        // The reader asked where the Logic App is, not which hosting mode somebody chose - so both modes
        // answer the same question rather than making everybody branch.
        => Assert.Equal(
            "https://invoke.example/workflow",
            await Read(
                AzureEnvironmentResourceKinds.LogicApp,
                "orders-flow",
                ValueNames.BaseUrl,
                ("LogicApp:orders-flow:HostingMode", "Consumption"),
                ("LogicApp:orders-flow:Consumption:InvokeUrl", "https://invoke.example/workflow")));

    [Fact]
    public async Task AWrittenCoordinateAnswersFromBothViewpoints()
    {
        // Whoever wrote one address wrote the address the thing answers on. Something that starts later and
        // really does have two publishes both and overrides these.
        Assert.Equal(
            "UseDevelopmentStorage=true",
            await Read(
                AzureEnvironmentResourceKinds.Storage,
                "files",
                ValueNames.ConnectionString,
                ResourceVantage.Network,
                ("StorageAccount:files:ConnectionString", "UseDevelopmentStorage=true")));
    }

    private Task<string?> Read(string kind, string identifier, string valueName, params (string Key, string Value)[] configured)
        => Read(kind, identifier, valueName, ResourceVantage.Host, configured);

    /// <summary>
    /// Everything a finished run says it knows, as a failure message or the frozen run would show it.
    /// </summary>
    private async Task<string> Listing(params (string Key, string Value)[] configured)
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new ReadsValueStep(AzureEnvironmentResourceKinds.FunctionApp, "orders-fn", ValueNames.BaseUrl, ResourceVantage.Host)).Name("reads")
            .Build();

        TimelineRun run = await timeline.SetupRun(Configured(configured), output.WriteLine).RunAsync();

        run.EnsureRanToCompletion();

        return string.Join(" | ", run.Values.Snapshot().Select(static value => $"{value.Key}={value.Value}"));
    }

    private async Task<string?> Read(
        string kind,
        string identifier,
        string valueName,
        ResourceVantage vantage,
        params (string Key, string Value)[] configured)
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new ReadsValueStep(kind, identifier, valueName, vantage)).Name("reads")
            .Build();

        TimelineRun run = await timeline.SetupRun(Configured(configured), output.WriteLine).RunAsync();

        run.EnsureRanToCompletion();

        return run.VariableStore.GetVariable<string>("value");
    }

    private static IServiceProvider Configured(params (string Key, string Value)[] values)
    {
        List<KeyValuePair<string, string?>> entries = [];

        foreach ((string key, string value) in values)
        {
            entries.Add(new KeyValuePair<string, string?>(key, value));
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(entries).Build();

        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.LoadAzureConfigs();

        return services.BuildServiceProvider();
    }

    /// <summary>Asks the run about a resource, rather than asking a configuration store.</summary>
    private sealed class ReadsValueStep(string kind, string identifier, string valueName, ResourceVantage vantage)
        : Step<EmptyStepResultContext>
    {
        public override string Name => "Reads a resource value";

        public override string Description => "Reads a configured resource's value from the run's resources.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new ReadsValueStep(kind, identifier, valueName, vantage).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            context.Variables.SetVariable("value", context.Values.Require(ValueRef.For(kind, identifier, valueName), vantage));

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }
}
