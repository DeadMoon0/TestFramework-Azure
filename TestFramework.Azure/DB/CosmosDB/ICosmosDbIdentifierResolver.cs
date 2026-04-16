using Microsoft.Azure.Cosmos;
using System;
using System.Linq;
using System.Reflection;

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
        PropertyInfo? prop = typeof(TItem).GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(x => string.Equals(x.Name, "id", System.StringComparison.OrdinalIgnoreCase));
        if (prop is null) throw new InvalidOperationException("Cannot find a fitting Id Property in Type (" + typeof(TItem).FullName + "). An Id is Required. Try add your own " + nameof(ICosmosDbIdentifierResolver));
        return prop.GetValue(item)?.ToString() ?? throw new InvalidOperationException("Cannot have an Id that is NULL. " + item?.ToString());
    }

    public PartitionKey ResolvePartitionKey<TItem>(TItem item)
    {
        PropertyInfo? prop = typeof(TItem).GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(x => string.Equals(x.Name, "partitionKey", System.StringComparison.OrdinalIgnoreCase));
        if (prop is null) throw new InvalidOperationException("Cannot find a fitting PartitionKey Property in Type (" + typeof(TItem).FullName + "). A PartitionKey is Required. Try add your own " + nameof(ICosmosDbIdentifierResolver));
        return new PartitionKey(prop.GetValue(item)?.ToString() ?? throw new InvalidOperationException("Cannot have a PartitionKey that is NULL. " + item?.ToString()));
    }
}