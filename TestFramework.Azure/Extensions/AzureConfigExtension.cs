using TestFramework.Azure.Configuration;
using TestFramework.Config.Builder.InstanceBuilder;

namespace TestFramework.Azure.Extensions;

/// <summary>
/// Extension methods for loading Azure configurations in timeline builders.
/// </summary>
public static class AzureConfigExtension
{
    /// <summary>
    /// Loads Azure configuration into the timeline config builder.
    /// </summary>
    /// <remarks>Uses <c>null</c> for <c>provider</c>; the other overload states it.</remarks>
    /// <param name="builder">The config instance builder.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public static IConfigInstanceBuilder LoadAzureConfig(this IConfigInstanceBuilder builder)
        => builder.LoadAzureConfig(null);

    /// <summary>
    /// Loads Azure configuration into the timeline config builder.
    /// </summary>
    /// <param name="builder">The config instance builder.</param>
    /// <param name="provider">Optional custom configuration provider. Uses DefaultConfigProvider if not specified.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public static IConfigInstanceBuilder LoadAzureConfig(this IConfigInstanceBuilder builder, IConfigProvider? provider)
    {
        builder.AddService(s => s.AddAzure(provider));
        return builder;
    }
}