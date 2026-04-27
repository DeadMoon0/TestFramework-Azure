namespace TestFramework.Azure.Identifier;

/// <summary>
/// Logical identifier used to resolve a configured Azure Storage Account.
/// </summary>
/// <param name="Identifier">The configuration key that names the Storage Account entry.</param>
public record StorageAccountIdentifier(string Identifier)
{
    /// <summary>
    /// Converts a typed identifier to its raw string representation.
    /// </summary>
    /// <param name="id">The typed identifier instance.</param>
    public static implicit operator string(StorageAccountIdentifier id) => id.Identifier;

    /// <summary>
    /// Converts a raw configuration key to a typed Storage Account identifier.
    /// </summary>
    /// <param name="id">The raw configuration key.</param>
    public static implicit operator StorageAccountIdentifier(string id) => new StorageAccountIdentifier(id);

    /// <summary>
    /// Returns the raw identifier value.
    /// </summary>
    public override string ToString() => Identifier;
}