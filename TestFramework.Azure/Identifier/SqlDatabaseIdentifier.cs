namespace TestFramework.Azure.Identifier;

public record SqlDatabaseIdentifier(string Identifier)
{
    public static implicit operator string(SqlDatabaseIdentifier id) => id.Identifier;
    public static implicit operator SqlDatabaseIdentifier(string id) => new SqlDatabaseIdentifier(id);

    public override string ToString() => Identifier;
}
