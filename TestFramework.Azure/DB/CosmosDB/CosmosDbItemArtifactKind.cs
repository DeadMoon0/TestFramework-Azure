using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.DB.CosmosDB;

public class CosmosDbItemArtifactKind<TItem> : ArtifactKind<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactReference<TItem>>, IStaticArtifactKind<CosmosDbItemArtifactKind<TItem>>
{
    public static CosmosDbItemArtifactKind<TItem> Kind { get; } = new CosmosDbItemArtifactKind<TItem>();
}