using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TestFramework.Azure.Runtime;
using Xunit.Abstractions;

namespace TestFramework.Azure.Tests;

/// <summary>
/// A client can only be built from coordinates the run stands behind.
/// </summary>
/// <remarks>
/// <para>
/// There are two records of every configuration type in play: the one somebody declared, which for anything
/// containerised is a placeholder, and the one the run resolved. They are the same type, so handing over the
/// wrong one compiled and read like every neighbouring line. It happened twice in one file while three
/// sibling cases in the same file had already been migrated, and the symptom was a step spending its whole
/// timeout dialling a default port.
/// </para>
/// <para>
/// So the factories stopped taking the record. What is checked here is the part a signature cannot state on
/// its own: that the token really is unforgeable, and that the next factory method somebody adds cannot
/// quietly go back to taking a record.
/// </para>
/// </remarks>
public class ResolvedConfigTests(ITestOutputHelper output)
{
    private static readonly Assembly Package = typeof(AzureResources).Assembly;

    private const string ConfigRecords = "TestFramework.Azure.Configuration.SpecificConfigs";

    [Fact]
    public void NoClientFactoryTakesARawConfigurationRecord()
    {
        // The rule the signatures express today, stated so a new method inherits it. A factory parameter is
        // either a resolved record or something that is not a configuration record at all - a table name, a
        // subscription name - and never the record itself.
        List<string> offenders = [];
        int checkedMethods = 0;

        foreach (Type factory in Package.GetTypes().Where(static type => type.IsInterface && type.Name.EndsWith("ComponentFactory", StringComparison.Ordinal)))
        {
            foreach (MethodInfo method in factory.GetMethods())
            {
                checkedMethods++;

                foreach (ParameterInfo parameter in method.GetParameters().Where(static parameter => parameter.ParameterType.Namespace == ConfigRecords))
                {
                    offenders.Add($"{factory.Name}.{method.Name} takes {parameter.ParameterType.Name} '{parameter.Name}' directly");
                }
            }
        }

        output.WriteLine($"checked {checkedMethods} factory methods");

        Assert.True(checkedMethods > 0, "the check found no factory methods at all, so it proved nothing");
        Assert.Empty(offenders);
    }

    [Fact]
    public void OnlyTheOneDoorCanMintAResolvedRecord()
    {
        // The guarantee behind the signatures. If any other type could produce one, the whole thing is a
        // naming convention again - wrap the store's record and carry on.
        List<Type> minters = [.. Package.GetTypes().Where(IsResolvedRecord)];

        foreach (Type minter in minters)
        {
            output.WriteLine($"{minter.Name} declared in {minter.DeclaringType?.Name ?? "(top level)"}");
        }

        Assert.NotEmpty(minters);
        Assert.All(minters, minter => Assert.Equal(typeof(AzureResources), minter.DeclaringType));
    }

    private static bool IsResolvedRecord(Type type)
    {
        for (Type? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Resolved<>))
            {
                return true;
            }
        }

        return false;
    }
}
