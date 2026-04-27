using System.Text.Json;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.DB.CosmosDB;

/// <summary>
/// Artifact payload that stores a Cosmos DB item snapshot.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
public class CosmosDbItemArtifactData<TItem>(TItem item) : ArtifactData<CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactReference<TItem>>
{
    /// <summary>
    /// The captured Cosmos item value.
    /// </summary>
    public TItem Item { get; } = item;

    /// <summary>
    /// Returns a readable string representation of the captured item.
    /// </summary>
    /// <returns>A string representation of the artifact data.</returns>
    public override string ToString()
    {
        try { return $"Cosmos DB Item<{typeof(TItem).Name}>: {JsonSerializer.Serialize(Item)}"; }
        catch { return $"Cosmos DB Item<{typeof(TItem).Name}>"; }
    }
}