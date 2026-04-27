using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.DB.CosmosDB;

/// <summary>
/// Static artifact kind for Cosmos DB item artifacts.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
public class CosmosDbItemArtifactKind<TItem> : ArtifactKind<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactReference<TItem>>, IStaticArtifactKind<CosmosDbItemArtifactKind<TItem>>
{
    /// <summary>
    /// Singleton artifact kind instance.
    /// </summary>
    public static CosmosDbItemArtifactKind<TItem> Kind { get; } = new CosmosDbItemArtifactKind<TItem>();
}