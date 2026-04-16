namespace TestFramework.Azure.Identifier;

public record FunctionAppIdentifier(string Identifier)
{
    public static implicit operator string(FunctionAppIdentifier id) => id.Identifier;
    public static implicit operator FunctionAppIdentifier(string id) => new FunctionAppIdentifier(id);

    public override string ToString() => Identifier;
}