namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// Configuration required to reach a Cosmos DB container used by Azure timeline artifacts and triggers.
/// </summary>
/// <remarks>
/// The identifier maps to a named entry under the <c>CosmosDb</c> section.
/// The first three properties are required because the runtime resolves a concrete container, not just an account.
/// </remarks>
public sealed record CosmosDbConfig
{
    // Its text redacts its credentials; see ConfigRecordText.
    private bool PrintMembers(System.Text.StringBuilder builder) => ConfigRecordText.PrintMembers(this, builder);

    /// <summary>
    /// Cosmos DB account connection string.
    /// </summary>
    [Secret]
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Database that contains the configured container.
    /// </summary>
    public required string DatabaseName { get; init; }

    /// <summary>
    /// Container used for reads, queries, and artifact setup/cleanup.
    /// </summary>
    public required string ContainerName { get; init; }

    /// <summary>
    /// Manual throughput, in request units per second, to provision when the framework creates the container.
    /// </summary>
    /// <remarks>
    /// Leave unset (the default) to let the account decide. This matters in both directions: a serverless
    /// account rejects a throughput request outright, and on a provisioned account any value set here is
    /// billed for as long as the container exists.
    /// </remarks>
    public int? ProvisionedThroughput { get; init; }

    /// <summary>
    /// Allows the framework to create the database and container when they do not exist yet.
    /// </summary>
    /// <remarks>
    /// Creation against a local emulator is always allowed. Against a real Cosmos account this must be
    /// opted into explicitly, so a mistyped connection string cannot silently provision resources in a
    /// shared or production account.
    /// </remarks>
    public bool AllowSchemaCreation { get; init; }
}
