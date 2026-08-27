namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// Configuration required to resolve a SQL database and its EF Core <c>DbContext</c>.
/// </summary>
/// <remarks>
/// The identifier maps to a named entry under the <c>SqlDatabase</c> section.
/// </remarks>
public record SqlDatabaseConfig
{
    /// <summary>
    /// Connection string used by the resolved <c>DbContext</c>.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Database name used for diagnostics and migration validation.
    /// </summary>
    public required string DatabaseName { get; init; }
}
