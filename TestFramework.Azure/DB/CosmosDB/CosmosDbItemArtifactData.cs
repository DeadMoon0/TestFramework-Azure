using System.Text.Json;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.DB.CosmosDB;

public class CosmosDbItemArtifactData<TItem>(TItem item) : ArtifactData<CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactReference<TItem>>
{
    public TItem Item { get; } = item;

    public override string ToString()
    {
        try { return $"Cosmos DB Item<{typeof(TItem).Name}>: {JsonSerializer.Serialize(Item)}"; }
        catch { return $"Cosmos DB Item<{typeof(TItem).Name}>"; }
    }
}