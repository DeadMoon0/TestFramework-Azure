namespace TestFramework.Azure.Identifier;

public record StorageAccountIdentifier(string Identifier)
{
    public static implicit operator string(StorageAccountIdentifier id) => id.Identifier;
    public static implicit operator StorageAccountIdentifier(string id) => new StorageAccountIdentifier(id);

    public override string ToString() => Identifier;
}