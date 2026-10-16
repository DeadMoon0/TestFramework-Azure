using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration.SpecificConfigs;

namespace TestFramework.Azure.DB.CosmosDb;

/// <summary>
/// Makes a Cosmos database and container exist, for a package that provisions the account they live in.
/// </summary>
/// <remarks>
/// <para>
/// This is a surface rather than a service: it names one thing a caller can ask for, and nothing about how
/// it is done. The REST client behind it stays internal, because how a container gets created - which API
/// version, which of two HTTP clients, whether the account even permits creation - is this package's
/// business and not something a caller should be able to reach, substitute, or depend on the shape of.
/// </para>
/// <para>
/// It exists because a container package needs exactly this one operation and was reaching it through an
/// <c>InternalsVisibleTo</c> grant. That grant made two packages special to each other: they understood
/// something a third package could not join in on, and what one of them could do stopped being what any of
/// them could do. A missing surface is the usual reason such a grant appears, and this is the surface that
/// was missing.
/// </para>
/// </remarks>
public static class CosmosSchema
{
    /// <summary>
    /// Ensures the database and container the configuration names both exist.
    /// </summary>
    /// <remarks>
    /// Idempotent, and cheap when there is nothing to do: the common case - the container is already there -
    /// costs one GET, and creation is attempted only after that comes back empty.
    /// </remarks>
    /// <param name="config">The resolved Cosmos container configuration.</param>
    /// <param name="partitionKeyPath">The container partition key path, including the leading slash.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>A task that completes when both exist.</returns>
    public static Task EnsureExistsAsync(
        CosmosDbConfig config,
        string partitionKeyPath,
        CancellationToken cancellationToken)
        => CosmosSchemaRestClient.EnsureDatabaseAndContainerExistAsync(config, partitionKeyPath, cancellationToken);
}
