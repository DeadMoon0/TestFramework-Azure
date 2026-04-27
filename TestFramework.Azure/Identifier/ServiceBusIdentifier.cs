namespace TestFramework.Azure.Identifier;

/// <summary>
/// Logical identifier used to resolve a configured Azure Service Bus resource.
/// </summary>
/// <param name="Identifier">The configuration key that names the Service Bus entry.</param>
public record ServiceBusIdentifier(string Identifier)
{
    /// <summary>
    /// Converts a typed identifier to its raw string representation.
    /// </summary>
    /// <param name="id">The typed identifier instance.</param>
    public static implicit operator string(ServiceBusIdentifier id) => id.Identifier;

    /// <summary>
    /// Converts a raw configuration key to a typed Service Bus identifier.
    /// </summary>
    /// <param name="id">The raw configuration key.</param>
    public static implicit operator ServiceBusIdentifier(string id) => new ServiceBusIdentifier(id);

    /// <summary>
    /// Returns the raw identifier value.
    /// </summary>
    public override string ToString() => Identifier;
}