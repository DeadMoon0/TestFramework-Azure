using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.StorageAccount.Blob;

/// <summary>
/// Static artifact kind for blob artifacts.
/// </summary>
public class StorageAccountBlobArtifactKind : ArtifactKind<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference>, IStaticArtifactKind<StorageAccountBlobArtifactKind>
{
    /// <summary>
    /// Singleton artifact kind instance.
    /// </summary>
    public static StorageAccountBlobArtifactKind Kind { get; } = new StorageAccountBlobArtifactKind();
}