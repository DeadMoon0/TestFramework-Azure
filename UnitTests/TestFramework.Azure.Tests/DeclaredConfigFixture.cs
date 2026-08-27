using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.Configuration;
using TestFramework.Config.Configuration;
using TestFramework.Core.Environment.Graph;

namespace TestFramework.Azure.Tests;

/// <summary>
/// Declares configuration records to a run the way a configuration file would.
/// </summary>
/// <remarks>
/// <para>
/// A fixture used to hand a step its configuration by registering a store, because a store was where a step
/// read from. It is not any more: a run's configuration is run data and travels as the run's resource values,
/// so a store registered in a container reached nothing, and the store itself is gone.
/// </para>
/// <para>
/// This keeps the convenient half of that idiom - building a record in C# rather than as configuration keys -
/// and sends it down the road the framework actually uses. What was a store the fixture filled is now just
/// the record and the name it goes by, which is all a store was ever carrying here.
/// </para>
/// </remarks>
internal static class DeclaredConfigFixture
{
    /// <summary>
    /// Declares one configuration record as the run's own resource values.
    /// </summary>
    /// <typeparam name="TConfig">The configuration record.</typeparam>
    /// <param name="services">The service collection the fixture is building.</param>
    /// <param name="identifier">Which resource the record describes.</param>
    /// <param name="config">The record.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection Declare<TConfig>(this IServiceCollection services, string identifier, TConfig config)
        where TConfig : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentNullException.ThrowIfNull(config);

        ConfigShape<TConfig> shape = AzureConfigShapes.For<TConfig>();

        // The shape answers both questions a run has: which kind these records are, and how to turn one back
        // into a record once the values are on the run.
        services.AddSingleton(shape);
        services.AddSingleton<IResourceNodeSource>(new RecordSource<TConfig>(shape, identifier, config));

        return services;
    }

    private sealed class RecordSource<TConfig>(ConfigShape<TConfig> shape, string identifier, TConfig config) : DeclaredNodeSource
        where TConfig : class
    {
        public override string SourceName => "test fixture";

        protected override IEnumerable<DeclaredResource> Declarations =>
            [new DeclaredResource(shape.Kind, identifier, shape.Values(config), this.SourceName)];
    }
}
