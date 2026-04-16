using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.StorageAccount.Blob;

public class StorageAccountBlobArtifactKind : ArtifactKind<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference>, IStaticArtifactKind<StorageAccountBlobArtifactKind>
{
    public static StorageAccountBlobArtifactKind Kind { get; } = new StorageAccountBlobArtifactKind();
}