using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using System.Reflection;
using System.Text.Json.Serialization;

namespace TestFramework.Azure.DB.CosmosDB;

public static class CosmosModelSchemaResolver
{
    public static string ResolveId<TItem>(TItem item)
    {
        return ResolvePropertyValue(item, ResolveIdProperty(typeof(TItem)), "Id");
    }

    public static PartitionKey ResolvePartitionKey<TItem>(TItem item)
    {
        return new PartitionKey(ResolvePropertyValue(item, ResolvePartitionKeyProperty(typeof(TItem)), "PartitionKey"));
    }

    public static string ResolvePartitionKeyPath<TItem>()
    {
        return ResolvePartitionKeyPath(typeof(TItem));
    }

    public static string ResolvePartitionKeyPath(Type itemType)
    {
        PropertyInfo property = ResolvePartitionKeyProperty(itemType);
        return "/" + ResolveSerializedPropertyName(property);
    }

    private static PropertyInfo ResolveIdProperty(Type itemType)
    {
        return ResolveProperty(
            itemType,
            propertyName: "id",
            errorMessage: $"Cannot find a fitting Id property in type '{itemType.FullName}'. A Cosmos model must expose an Id property or map one via JsonProperty/JsonPropertyName.");
    }

    private static PropertyInfo ResolvePartitionKeyProperty(Type itemType)
    {
        return ResolveProperty(
            itemType,
            propertyName: "partitionKey",
            errorMessage: $"Cannot find a fitting PartitionKey property in type '{itemType.FullName}'. A Cosmos model must expose a PartitionKey property or map one via JsonProperty/JsonPropertyName.");
    }

    private static PropertyInfo ResolveProperty(Type itemType, string propertyName, string errorMessage)
    {
        PropertyInfo? property = itemType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(candidate => new
            {
                Property = candidate,
                SerializedName = ResolveSerializedPropertyName(candidate),
            })
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.SerializedName, propertyName, StringComparison.OrdinalIgnoreCase))
            ?.Property;

        return property ?? throw new InvalidOperationException(errorMessage);
    }

    private static string ResolvePropertyValue<TItem>(TItem item, PropertyInfo property, string logicalName)
    {
        return property.GetValue(item)?.ToString()
            ?? throw new InvalidOperationException($"Cannot have a {logicalName} that is NULL. {item}");
    }

    private static string ResolveSerializedPropertyName(PropertyInfo property)
    {
        JsonPropertyAttribute? newtonsoftAttribute = property.GetCustomAttribute<JsonPropertyAttribute>();
        if (!string.IsNullOrWhiteSpace(newtonsoftAttribute?.PropertyName))
            return newtonsoftAttribute.PropertyName!;

        JsonPropertyNameAttribute? systemTextAttribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (!string.IsNullOrWhiteSpace(systemTextAttribute?.Name))
            return systemTextAttribute.Name;

        return property.Name;
    }
}