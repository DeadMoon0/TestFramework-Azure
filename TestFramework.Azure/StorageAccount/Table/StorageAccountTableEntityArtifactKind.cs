using Azure.Data.Tables;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.StorageAccount.Table;

/// <summary>
/// Static artifact kind for Azure Table entity artifacts.
/// </summary>
/// <typeparam name="T">The table entity type.</typeparam>
public class StorageAccountTableEntityArtifactKind<T> : ArtifactKind<StorageAccountTableEntityArtifactDescriber<T>, StorageAccountTableEntityArtifactData<T>, StorageAccountTableEntityArtifactReference<T>>, IStaticArtifactKind<StorageAccountTableEntityArtifactKind<T>>
    where T : class, ITableEntity
{
    /// <summary>
    /// Singleton artifact kind instance.
    /// </summary>
    public static StorageAccountTableEntityArtifactKind<T> Kind { get; } = new StorageAccountTableEntityArtifactKind<T>();
}
