namespace TestFramework.Azure.Identifier;

/// <summary>
/// Logical identifier used to resolve a configured Azure Function App.
/// </summary>
/// <param name="Identifier">The configuration key that names the Function App entry.</param>
public record FunctionAppIdentifier(string Identifier)
{
    /// <summary>
    /// Converts a typed identifier to its raw string representation.
    /// </summary>
    /// <param name="id">The typed identifier instance.</param>
    public static implicit operator string(FunctionAppIdentifier id) => id.Identifier;

    /// <summary>
    /// Converts a raw configuration key to a typed Function App identifier.
    /// </summary>
    /// <param name="id">The raw configuration key.</param>
    public static implicit operator FunctionAppIdentifier(string id) => new FunctionAppIdentifier(id);

    /// <summary>
    /// Returns the raw identifier value.
    /// </summary>
    public override string ToString() => Identifier;
}