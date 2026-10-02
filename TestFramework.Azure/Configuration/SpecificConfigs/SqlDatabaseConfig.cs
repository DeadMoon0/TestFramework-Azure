namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// Configuration required to resolve a SQL database and its EF Core <c>DbContext</c>.
/// </summary>
/// <remarks>
/// The identifier maps to a named entry under the <c>SqlDatabase</c> section.
/// </remarks>
public sealed record SqlDatabaseConfig
{
    // Its text redacts its credentials; see ConfigRecordText.
    private bool PrintMembers(System.Text.StringBuilder builder) => ConfigRecordText.PrintMembers(this, builder);

    /// <summary>
    /// Connection string used by the resolved <c>DbContext</c>.
    /// </summary>
    [Secret]
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Database name used for diagnostics and migration validation.
    /// </summary>
    public required string DatabaseName { get; init; }
}
