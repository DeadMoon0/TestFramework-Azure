using System;
using System.IO;
using System.Linq;
using Microsoft.Azure.Cosmos;
using TestFramework.Azure.DB.CosmosDb;
using TestFramework.Core.Conventions;
using Xunit.Abstractions;

namespace TestFramework.Azure.Tests;

/// <summary>
/// The family's rules, checked against this package rather than trusted to have been followed.
/// </summary>
/// <remarks>
/// These live in Core (<see cref="StepConventions"/>) and every package calls them on its own assembly: a
/// rule only Core's suite enforces is a rule only Core follows.
/// </remarks>
public class ConventionTests(ITestOutputHelper output)
{
    [Fact]
    public void EveryStepInThisPackageClonesItself()
    {
        // A step that inherits a concrete base class's Clone() runs as that base class and silently loses
        // whatever it added. The compiler only catches the case where the base is abstract.
        ConventionReport report = StepConventions.AssertEveryStepClonesItself(typeof(CosmosModelSchemaResolver).Assembly);

        output.WriteLine(report.ToString());
        Assert.True(report.Checked > 0, "the check found no steps at all, so it proved nothing");
    }

    [Fact]
    public void FreezingCascadesThroughThisPackagesParts()
    {
        ConventionReport report = StepConventions.AssertFreezingCascades(typeof(CosmosModelSchemaResolver).Assembly);

        output.WriteLine(report.ToString());
        foreach (string skipped in report.Skipped)
        {
            output.WriteLine($"  skipped {skipped}");
        }
    }

    [Fact]
    public void OnlyTheCosmosAttributeReaderNamesSystemTextJson()
    {
        // The rule itself, not only its one exception: the family serialises with Newtonsoft (ARCHITECTURE.md
        // section 6). System.Text.Json ships in the shared framework every package compiles against, so the reference
        // cannot be what the check looks at; the source can. Reading a test author's Cosmos attributes is the one use, and anything
        // else - a serialiser call slipped in beside it - is what review cannot see and this test does.
        string package = Path.Combine(RepositoryRoot(), "TestFramework.Azure");
        string[] users = [.. Directory.GetFiles(package, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("System.Text.Json", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OfType<string>()];

        Assert.Equal(["CosmosModelSchemaResolver.cs"], users);
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.Azure.Functions.Worker")]
    public void ThisPackageNeedsNeitherAspNetCoreNorTheFunctionsWorker(string family)
    {
        // A consumer that only calls deployed Function Apps must not be handed ASP.NET Core and the Functions Worker.
        // What needs them - in-process execution, reading a function's attributes - is TestFramework.Azure.Functions.
        string[] references = [.. typeof(CosmosModelSchemaResolver).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .Where(name => name == family || name.StartsWith(family + ".", StringComparison.Ordinal))];

        Assert.Empty(references);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TestFramework.Azure.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException($"No TestFramework.Azure.slnx above '{AppContext.BaseDirectory}'.");
    }

    [Fact]
    public void ACosmosModelMayBeAnnotatedWithEitherJsonLibrary()
    {
        // Why this package still names System.Text.Json, and the only reason it may.
        //
        // The family serialises with Newtonsoft only (ARCHITECTURE.md section 6), and nothing here
        // serialises with anything else. But a Cosmos model belongs to the test author, not to us, and
        // theirs may be annotated with either library's attribute. Reading both is interop; dropping the
        // System.Text.Json one would silently retarget somebody's container to the wrong property path.
        //
        // So the assertion is behavioural rather than a check on assembly references: it pins the reason
        // the reference exists. If this ever stops being the reason, the reference should go.
        Assert.Equal("/newtonsoftKey", CosmosModelSchemaResolver.ResolvePartitionKeyPath<NewtonsoftAnnotatedModel>());
        Assert.Equal("/systemTextKey", CosmosModelSchemaResolver.ResolvePartitionKeyPath<SystemTextAnnotatedModel>());

        Assert.Equal(new PartitionKey("tenant-1"), CosmosModelSchemaResolver.ResolvePartitionKey(new NewtonsoftAnnotatedModel()));
        Assert.Equal(new PartitionKey("tenant-2"), CosmosModelSchemaResolver.ResolvePartitionKey(new SystemTextAnnotatedModel()));
    }

    private sealed class NewtonsoftAnnotatedModel
    {
        [Newtonsoft.Json.JsonProperty("newtonsoftId")]
        public string Id { get; set; } = "1";

        [Newtonsoft.Json.JsonProperty("newtonsoftKey")]
        public string PartitionKey { get; set; } = "tenant-1";
    }

    private sealed class SystemTextAnnotatedModel
    {
        [System.Text.Json.Serialization.JsonPropertyName("systemTextId")]
        public string Id { get; set; } = "2";

        [System.Text.Json.Serialization.JsonPropertyName("systemTextKey")]
        public string PartitionKey { get; set; } = "tenant-2";
    }

    [Fact]
    public void ThisPackageKeepsItsInternalsToItself()
    {
        // Every package is a stranger to every other. A grant to another package is a private handshake:
        // two packages understand each other and a third cannot join, so what the favoured one may do stops
        // being what any of them may do - and the grant hides the fact that a surface is missing.
        ConventionReport report = StepConventions.AssertNoPackageSeesAnothersInternals(typeof(CosmosModelSchemaResolver).Assembly);

        output.WriteLine(report.ToString());
    }

    [Fact]
    public void ThisPackagesPublicSurfaceCanGrow()
    {
        // Each package is compiled on its own. A knob added as one more optional parameter, or one more member
        // on a positional record, compiles everywhere and then throws MissingMethodException in every package
        // built against the old signature.
        ConventionReport report = StepConventions.AssertPublicSurfaceCanGrow(typeof(CosmosModelSchemaResolver).Assembly);

        output.WriteLine(report.ToString());
    }

    [Fact]
    public void EveryExceptionThisPackageThrowsIsAFrameworkException()
    {
        // One base, so a failure always carries its recovery steps and reads the same way in a run's failure detail.
        // Two used to derive from bare Exception, and one of them said only "Code cannot be Empty or Null".
        string[] strays = [.. typeof(CosmosModelSchemaResolver).Assembly.GetTypes()
            .Where(type => type.IsPublic && typeof(System.Exception).IsAssignableFrom(type))
            .Where(type => !typeof(TestFramework.Core.Exceptions.TimelineFrameworkException).IsAssignableFrom(type))
            .Select(type => type.FullName!)];

        Assert.Empty(strays);
    }
}
