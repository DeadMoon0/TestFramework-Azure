using Microsoft.Azure.Cosmos;

namespace TestFramework.Azure.DB.CosmosDB;

/// <summary>
/// Resolves document identity fields used by Cosmos DB artifacts and query finders.
/// </summary>
public interface ICosmosDbIdentifierResolver
{
    /// <summary>
    /// Resolves the document identifier for an item.
    /// </summary>
    /// <typeparam name="TItem">The item type being inspected.</typeparam>
    /// <param name="item">The item instance.</param>
    /// <returns>The logical Cosmos document id.</returns>
    public string ResolveId<TItem>(TItem item);

    /// <summary>
    /// Resolves the partition key for an item.
    /// </summary>
    /// <typeparam name="TItem">The item type being inspected.</typeparam>
    /// <param name="item">The item instance.</param>
    /// <returns>The partition key used to address the item.</returns>
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