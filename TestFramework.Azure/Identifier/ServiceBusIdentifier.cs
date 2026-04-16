namespace TestFramework.Azure.Identifier;

public record ServiceBusIdentifier(string Identifier)
{
    public static implicit operator string(ServiceBusIdentifier id) => id.Identifier;
    public static implicit operator ServiceBusIdentifier(string id) => new ServiceBusIdentifier(id);

    public override string ToString() => Identifier;
}