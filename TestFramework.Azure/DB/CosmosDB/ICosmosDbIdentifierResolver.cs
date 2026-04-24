using Microsoft.Azure.Cosmos;

namespace TestFramework.Azure.DB.CosmosDB;

public interface ICosmosDbIdentifierResolver
{
    public string ResolveId<TItem>(TItem item);
    public PartitionKey ResolvePartitionKey<TItem>(TItem item);
}

internal class DefaultCosmosDbIdentifierResolver : ICosmosDbIdentifierResolver
{
    public string ResolveId<TItem>(TItem item)
    {
        return CosmosModelSchemaResolver.ResolveId(item);
    }

    public PartitionKey ResolvePartitionKey<TItem>(TItem item)
    {
        return CosmosModelSchemaResolver.ResolvePartitionKey(item);
    }
}