namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// Configuration required to resolve a SQL database and its EF Core <c>DbContext</c>.
/// </summary>
/// <remarks>
/// The identifier maps to a named entry under the <c>SqlDatabase</c> section.
/// <see cref="ContextType"/> is optional when the context is registered by identifier or as the default SQL context.
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
    /// <summary>
    /// Optional. Assembly-qualified CLR type name of the DbContext to use.
    /// When set, overrides any identifier-based registration.
    /// When absent, falls back to the identifier-based or default registration.
    /// Example: "MyProject.Data.MainDbContext, MyProject"
    /// </summary>
    public string? ContextType { get; init; }
}
