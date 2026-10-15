using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Extensions;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Azure.Tests;

/// <summary>
/// Where a storage account is, when a container knows and the file does not.
/// </summary>
/// <remarks>
/// <para>
/// This is the first resource in the family whose coordinates travel as a produced value rather than by a
/// starting container writing back into this package's configuration store. Two things have to hold for that
/// to be an improvement rather than a rearrangement: what the container publishes must win over what the file
/// said, and the two directions of the mapping - the shape that puts a record into the graph, and the resolver
/// that reads one back out - must agree.
/// </para>
/// <para>
/// The second is what the round-trip case is for. Those two mappings are written in different files and
/// nothing but this stops them drifting apart.
/// </para>
/// </remarks>
public class StorageAccountResourceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task WhatAContainerPublishesWinsOverWhatTheFileSaid()
    {
        // The whole point. The file has a placeholder; something started and learned the real port.
        TimelineRun run = await Run(
            new PublishingEnvironment("files", "DefaultEndpointsProtocol=http;AccountName=real;BlobEndpoint=http://127.0.0.1:54321/"),
            ("StorageAccount:files:ConnectionString", "UseDevelopmentStorage=true"),
            ("StorageAccount:files:BlobContainerName", "documents"));

        run.EnsureRanToCompletion();

        Assert.Equal(
            "DefaultEndpointsProtocol=http;AccountName=real;BlobEndpoint=http://127.0.0.1:54321/",
            run.VariableStore.GetVariable<string>("connection"));

        // Not published, so it still comes from the declaration - a produced value replaces one value, not
        // the entry.
        Assert.Equal("documents", run.VariableStore.GetVariable<string>("container"));
    }

    [Fact]
    public async Task WithNothingPublishedTheDeclarationStillAnswers()
    {
        // A deployed account nobody containerises: the file is the only source, and reading through the run
        // has to work exactly as well.
        TimelineRun run = await Run(
            environment: null,
            ("StorageAccount:files:ConnectionString", "UseDevelopmentStorage=true"),
            ("StorageAccount:files:BlobContainerName", "documents"));

        run.EnsureRanToCompletion();

        Assert.Equal("UseDevelopmentStorage=true", run.VariableStore.GetVariable<string>("connection"));
        Assert.Equal("documents", run.VariableStore.GetVariable<string>("container"));
    }

    [Fact]
    public async Task TheShapeAndTheResolverAgreeOnEveryValue()
    {
        // The two directions of one mapping, written in different files. If the shape publishes a value under a
        // name the resolver does not read - or the other way round - this is what says so.
        TimelineRun run = await Run(
            environment: null,
            ("StorageAccount:files:ConnectionString", "UseDevelopmentStorage=true"),
            ("StorageAccount:files:BlobContainerName", "blobs"),
            ("StorageAccount:files:QueueContainerName", "queues"),
            ("StorageAccount:files:TableContainerName", "tables"));

        run.EnsureRanToCompletion();

        Assert.Equal("UseDevelopmentStorage=true", run.VariableStore.GetVariable<string>("connection"));
        Assert.Equal("blobs", run.VariableStore.GetVariable<string>("container"));
        Assert.Equal("queues", run.VariableStore.GetVariable<string>("queue"));
        Assert.Equal("tables", run.VariableStore.GetVariable<string>("table"));
    }

    private async Task<TimelineRun> Run(PublishingEnvironment? environment, params (string Key, string Value)[] configured)
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new ResolvesStorageStep("files")).Label("resolves")
            .Build();

        ITimelineRunBuilder builder = timeline.SetupRun(Configured(configured), output.WriteLine);

        if (environment is not null)
        {
            builder = builder.SetEnv(environment);
        }

        return await builder.RunAsync();
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

    /// <summary>Stands in for Azurite: something that starts and then knows where it is.</summary>
    private sealed class PublishingEnvironment(string identifier, string connectionString) : EnvironmentProviderBase
    {
        private readonly EnvComponentIdentifier component = "storage";

        public PublishingEnvironment()
            : this("files", "UseDevelopmentStorage=true")
        {
        }

        public override IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(
            IEnumerable<Core.Artifacts.ArtifactInstanceGeneric> artifacts,
            IEnumerable<EnvironmentRequirement> requirements)
        {
            this.AddComponent(new PublishingComponent(this.component, identifier, connectionString));

            return [this.component];
        }
    }

    private sealed class PublishingComponent(EnvComponentIdentifier id, string identifier, string connectionString) : EnvComponent
    {
        public override EnvComponentIdentifier Id => id;

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
        {
            context.Resources?.Produce(
                AzureEnvironmentResourceKinds.StorageKind,
                identifier,
                ValueNames.ConnectionString,
                ResourceVantage.Host,
                connectionString);

            return Task.FromResult<object?>(null);
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context)
            => Task.CompletedTask;
    }

    /// <summary>Resolves the account the way every storage consumer in the package now does.</summary>
    private sealed class ResolvesStorageStep(string identifier) : Step<EmptyStepResultContext>
    {
        public override string Name => "Resolves storage";

        public override string Description => "Resolves a storage account through the run.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new ResolvesStorageStep(identifier).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            StorageAccountConfig resolved = context.Configured<StorageAccountConfig>(identifier);

            context.Variables.SetVariable("connection", resolved.ConnectionString);
            context.Variables.SetVariable("container", resolved.BlobContainerName);
            context.Variables.SetVariable("queue", resolved.QueueContainerName);
            context.Variables.SetVariable("table", resolved.TableContainerName);

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }
}
