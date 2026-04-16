namespace TestFramework.Azure.Configuration.SpecificConfigs;

public record SqlDatabaseConfig
{
    public required string ConnectionString { get; init; }
    public required string DatabaseName { get; init; }
    /// <summary>
    /// Optional. Assembly-qualified CLR type name of the DbContext to use.
    /// When set, overrides any identifier-based registration.
    /// When absent, falls back to the identifier-based or default registration.
    /// Example: "MyProject.Data.MainDbContext, MyProject"
    /// </summary>
    public string? ContextType { get; init; }
}
