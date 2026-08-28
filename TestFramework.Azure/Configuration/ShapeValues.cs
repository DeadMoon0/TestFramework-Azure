using System.Collections.Generic;
using TestFramework.Core.Exceptions;

namespace TestFramework.Azure.Configuration;

/// <summary>
/// How a shape reads the run's flattened values: one rule for emptiness, one refusal per failure kind.
/// </summary>
/// <remarks>
/// One home instead of a private copy per shape. "Empty string means absent", the flag-parsing rule
/// and the two refusal shapes lived in five copies, which is the drift §4 names - two of the copies
/// already carried dead members, and a sixth shape would have written a seventh rule. The shapes pull
/// these in with <c>using static</c>, so their read methods keep reading the same.
/// </remarks>
internal static class ShapeValues
{
    /// <summary>
    /// The value under <paramref name="valueName"/>, with empty meaning absent.
    /// </summary>
    internal static string? Text(IReadOnlyDictionary<string, string> values, string valueName)
        => values.TryGetValue(valueName, out string? value) && value is { Length: > 0 } ? value : null;

    /// <summary>
    /// The flag under <paramref name="valueName"/>, refusing by name what it cannot read.
    /// </summary>
    internal static bool Flag(IReadOnlyDictionary<string, string> values, string valueName, string identifier, bool fallback)
    {
        if (!values.TryGetValue(valueName, out string? value))
        {
            return fallback;
        }

        return bool.TryParse(value, out bool parsed)
            ? parsed
            : throw Unreadable(value, identifier, valueName, "'true' or 'false'");
    }

    /// <summary>
    /// The refusal for a value that exists but cannot be read as what the shape needs.
    /// </summary>
    internal static FrameworkConfigurationException Unreadable(string value, string identifier, string valueName, string expected)
        => new FrameworkConfigurationException(
            $"The run holds '{value}' as {valueName} for '{identifier}', which cannot be read as {expected}.",
            [
                $"Correct the configuration entry, or the environment component that publishes it.",
            ]);

    /// <summary>
    /// The refusal for a required value the run does not hold, listing what it does.
    /// </summary>
    internal static FrameworkConfigurationException Missing(string valueName, string identifier, IReadOnlyDictionary<string, string> values)
        => new FrameworkConfigurationException(
            $"The run has no {valueName} for '{identifier}'.",
            [
                "Add it to the configuration entry, or include the definition that provisions the resource.",
            ],
            [.. values.Keys]);
}
