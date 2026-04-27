namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// Configuration required to reach a Cosmos DB container used by Azure timeline artifacts and triggers.
/// </summary>
/// <remarks>
/// The identifier maps to a named entry under the <c>CosmosDb</c> section.
/// All three properties are required because the runtime resolves a concrete container, not just an account.
/// </remarks>
public record CosmosContainerDbConfig
{
    /// <summary>
    /// Cosmos DB account connection string.
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Database that contains the configured container.
    /// </summary>
    public required string DatabaseName { get; set; }

    /// <summary>
    /// Container used for reads, queries, and artifact setup/cleanup.
    /// </summary>
    public required string ContainerName { get; set; }
}