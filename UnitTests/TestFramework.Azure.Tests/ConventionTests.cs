using System.Linq;
using Microsoft.Azure.Cosmos;
using TestFramework.Azure.DB.CosmosDB;
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
    public void ACosmosModelMayBeAnnotatedWithEitherJsonLibrary()
    {
        // Why this package still names System.Text.Json, and the only reason it may.
        //
        // The family serialises with Newtonsoft only (ARCHITECTURE.md section 4), and nothing here
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
}
