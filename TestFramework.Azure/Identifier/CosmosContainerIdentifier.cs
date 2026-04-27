namespace TestFramework.Azure.Identifier;

/// <summary>
/// Logical identifier used to resolve a configured Cosmos DB container.
/// </summary>
/// <param name="Identifier">The configuration key that names the Cosmos DB entry.</param>
public record CosmosContainerIdentifier(string Identifier)
{
    /// <summary>
    /// Converts a typed identifier to its raw string representation.
    /// </summary>
    /// <param name="id">The typed identifier instance.</param>
    public static implicit operator string(CosmosContainerIdentifier id) => id.Identifier;

    /// <summary>
    /// Converts a raw configuration key to a typed Cosmos DB identifier.
    /// </summary>
    /// <param name="id">The raw configuration key.</param>
    public static implicit operator CosmosContainerIdentifier(string id) => new CosmosContainerIdentifier(id);

    /// <summary>
    /// Returns the raw identifier value.
    /// </summary>
    public override string ToString() => Identifier;
}