namespace TestFramework.Azure.Identifier;

/// <summary>
/// Logical identifier used to resolve a configured Azure Logic App.
/// </summary>
/// <param name="Identifier">The configuration key that names the Logic App entry.</param>
public record LogicAppIdentifier(string Identifier)
{
    /// <summary>
    /// Converts a typed identifier to its raw string representation.
    /// </summary>
    /// <param name="id">The typed identifier instance.</param>
    public static implicit operator string(LogicAppIdentifier id) => id.Identifier;

    /// <summary>
    /// Converts a raw configuration key to a typed Logic App identifier.
    /// </summary>
    /// <param name="id">The raw configuration key.</param>
    public static implicit operator LogicAppIdentifier(string id) => new(id);

    /// <summary>
    /// Returns the raw identifier value.
    /// </summary>
    public override string ToString() => Identifier;
}