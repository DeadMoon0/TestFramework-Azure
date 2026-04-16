namespace TestFramework.Azure.Identifier;

public record CosmosContainerIdentifier(string Identifier)
{
    public static implicit operator string(CosmosContainerIdentifier id) => id.Identifier;
    public static implicit operator CosmosContainerIdentifier(string id) => new CosmosContainerIdentifier(id);

    public override string ToString() => Identifier;
}