namespace TestFramework.Azure.Identifier;

/// <summary>
/// Logical identifier used to resolve a configured SQL database.
/// </summary>
/// <param name="Identifier">The configuration key that names the SQL database entry.</param>
public record SqlDatabaseIdentifier(string Identifier)
{
    /// <summary>
    /// Converts a typed identifier to its raw string representation.
    /// </summary>
    /// <param name="id">The typed identifier instance.</param>
    public static implicit operator string(SqlDatabaseIdentifier id) => id.Identifier;

    /// <summary>
    /// Converts a raw configuration key to a typed SQL database identifier.
    /// </summary>
    /// <param name="id">The raw configuration key.</param>
    public static implicit operator SqlDatabaseIdentifier(string id) => new SqlDatabaseIdentifier(id);

    /// <summary>
    /// Returns the raw identifier value.
    /// </summary>
    public override string ToString() => Identifier;
}
