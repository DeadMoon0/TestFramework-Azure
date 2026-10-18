using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TestFramework.Core.Conventions;
using Xunit.Abstractions;

namespace TestFramework.Azure.Functions.Tests;

/// <summary>
/// The family's rules, checked against this package rather than trusted to have been followed.
/// </summary>
/// <remarks>
/// These live in Core (<see cref="StepConventions"/>) and every package calls them on its own assembly: a rule only
/// Core's suite enforces is a rule only Core follows.
/// </remarks>
public class ConventionTests(ITestOutputHelper output)
{
    private static readonly Assembly Package = typeof(FunctionAppTriggerExtensions).Assembly;

    [Fact]
    public void EveryStepInThisPackageClonesItself()
    {
        ConventionReport report = StepConventions.AssertEveryStepClonesItself(Package);

        output.WriteLine(report.ToString());
        Assert.True(report.Checked > 0, "the check found no steps at all, so it proved nothing");
    }

    [Fact]
    public void FreezingCascadesThroughThisPackagesParts()
    {
        ConventionReport report = StepConventions.AssertFreezingCascades(Package);

        output.WriteLine(report.ToString());
    }

    [Fact]
    public void ThisPackageKeepsItsInternalsToItself()
    {
        ConventionReport report = StepConventions.AssertNoPackageSeesAnothersInternals(Package);

        output.WriteLine(report.ToString());
    }

    [Fact]
    public void ThisPackagesPublicSurfaceCanGrow()
    {
        ConventionReport report = StepConventions.AssertPublicSurfaceCanGrow(Package);

        output.WriteLine(report.ToString());
    }

    [Fact]
    public void EveryExceptionTypeThisPackageDeclaresIsAFrameworkException()
    {
        string[] strays = [.. Package.GetTypes()
            .Where(type => type.IsPublic && typeof(Exception).IsAssignableFrom(type))
            .Where(type => !typeof(TestFramework.Core.Exceptions.TimelineFrameworkException).IsAssignableFrom(type))
            .Select(type => type.FullName!)];

        Assert.Empty(strays);
    }

    [Fact]
    public void NothingInThisPackageNamesSystemTextJson()
    {
        // The family serialises with Newtonsoft (ARCHITECTURE.md section 6). ASP.NET Core brings System.Text.Json
        // along, so the reference cannot be what the check looks at; the source can. An in-process response body is
        // written with Newtonsoft set to the host's naming, and nothing else here serialises.
        string package = Path.Combine(RepositoryRoot(), "TestFramework.Azure.Functions");
        string[] users = [.. Directory.GetFiles(package, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("System.Text.Json", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OfType<string>()];

        Assert.Empty(users);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TestFramework.Azure.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException($"No TestFramework.Azure.slnx above '{AppContext.BaseDirectory}'.");
    }
}
