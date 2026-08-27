using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.DB.SqlServer;
using TestFramework.Azure.Extensions;
using TestFramework.Core.Environment;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Azure.Tests;

/// <summary>
/// A DbContext built against the database the run is actually using.
/// </summary>
/// <remarks>
/// <para>
/// This was the last place in the family where a starting container wrote its address into somebody else's
/// configuration store, and the reason was EF: <c>AddDbContext</c> takes a connection string when the
/// registration is built, from a service provider, with no run in sight. So the store was the only channel
/// that reached one.
/// </para>
/// <para>
/// The registration now says how to <em>construct</em> a context and is handed options the framework has
/// already pointed at the run's database. What that buys is not tidiness: a context aimed at the wrong
/// database stops being something a caller can express, which is the same shape as <c>Resolved&lt;T&gt;</c>
/// on the client factories.
/// </para>
/// <para>
/// The alternative - resolving the context from DI and redirecting it afterwards with
/// <c>Database.SetConnectionString</c> - would have left "always go through the resolver" as a rule nothing
/// enforces, and would have mutated an instance somebody else already held, since <c>AddDbContext</c>
/// registers scoped.
/// </para>
/// </remarks>
public class SqlDbContextRegistryTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AContextIsBuiltAgainstWhatTheRunPublished()
    {
        // The whole point. The file carries a placeholder; something started and learned the real port, and
        // the context that comes out is pointed at the second one without the caller doing anything.
        TimelineRun run = await Run(new PublishingEnvironment("orders", "Server=127.0.0.1,49999;Database=orders;User Id=sa;Password=x"));

        run.EnsureRanToCompletion();

        Assert.Equal(
            "Server=127.0.0.1,49999;Database=orders;User Id=sa;Password=x",
            run.VariableStore.GetVariable<string>("connection"));
    }

    [Fact]
    public async Task WithNothingPublishedTheDeclaredEntryBuildsIt()
    {
        // A deployed database: the entry is the only source, and building a context from it has to work
        // exactly as well as reading the store did.
        TimelineRun run = await Run(environment: null);

        run.EnsureRanToCompletion();

        Assert.Contains("localhost", run.VariableStore.GetVariable<string>("connection")!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACallersOwnOptionsSurviveBesideTheConnection()
    {
        // The framework owns the coordinate and the caller owns the rest - so a configure callback has to
        // actually reach a real builder, or "everything else is yours" would be a claim rather than a fact.
        // Asserted on the callback rather than by reading the options back: what the options then hold is
        // EF's business, and prying it out means reaching for EF internals.
        bool configured = false;

        TimelineRun run = await Run(
            new PublishingEnvironment("orders", "Server=127.0.0.1,49999;Database=orders"),
            configure: (services, builder) =>
            {
                // A real builder, and the run's services beside it - so an interceptor or a logger the caller
                // registered is reachable from here.
                Assert.NotNull(services);
                builder.EnableSensitiveDataLogging();
                configured = true;
            });

        run.EnsureRanToCompletion();

        Assert.True(configured, "the configure callback never ran, so 'everything else is yours' is not true");

        // And it did not cost the coordinate: the callback runs after the connection is set, which is the
        // ordering the design rests on - a caller who saw an unset connection would reasonably set one.
        Assert.Equal("Server=127.0.0.1,49999;Database=orders", run.VariableStore.GetVariable<string>("connection"));
    }

    [Fact]
    public async Task AnIdentifierNobodyRegisteredIsRefusedByName()
    {
        TimelineRun run = await Run(environment: null, resolves: "invoices");

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        Assert.Contains("invoices", failure.ToString(), StringComparison.Ordinal);
        Assert.Contains("AddForIdentifier", failure.ToString(), StringComparison.Ordinal);
    }

    private async Task<TimelineRun> Run(
        PublishingEnvironment? environment,
        string resolves = "orders",
        Action<IServiceProvider, DbContextOptionsBuilder<ProbeContext>>? configure = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("SqlDatabase:orders:ConnectionString", "Server=localhost,1433;Database=orders"),
                new KeyValuePair<string, string?>("SqlDatabase:orders:DatabaseName", "orders"),
            ])
            .Build();

        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.LoadAzureConfigs(configuration);
        services.AddSqlArtifactContexts(registry => registry.AddForIdentifier<ProbeContext>(
            "orders",
            options => new ProbeContext(options),
            configure));

        Timeline timeline = Timeline.Create()
            .Trigger(new BuildsContextStep(resolves)).Name("builds")
            .Build();

        ITimelineRunBuilder builder = timeline.SetupRun(services.BuildServiceProvider(), output);

        if (environment is not null)
        {
            builder = builder.SetEnv(environment);
        }

        return await builder.RunAsync();
    }

    /// <summary>Stands in for the SQL container: it starts, then it knows its port.</summary>
    private sealed class PublishingEnvironment(string identifier, string connectionString) : EnvironmentProviderBase
    {
        private readonly EnvComponentIdentifier component = "sql";

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
            PublishOn(context).Produce(
                AzureEnvironmentResourceKinds.SqlKind,
                identifier,
                ValueNames.ConnectionString,
                ResourceVantage.Host,
                connectionString);

            return Task.FromResult<object?>(null);
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context)
            => Task.CompletedTask;
    }

    /// <summary>A context that is never connected to - only asked what it was pointed at.</summary>
    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options)
    {
    }

    private sealed class BuildsContextStep(string identifier) : Step<EmptyStepResultContext>
    {
        public override string Name => "Builds a DbContext";

        public override string Description => "Builds a DbContext through the SQL registry.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new BuildsContextStep(identifier).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            ISqlDbContextResolver resolver = context.Services.GetRequiredService<ISqlDbContextResolver>();

            using DbContext built = resolver.Resolve(context, identifier);

            context.Variables.SetVariable("connection", built.Database.GetConnectionString());

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }
}
