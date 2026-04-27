using Azure.Data.Tables;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.StorageAccount.Table;

/// <summary>
/// Static artifact kind for Azure Table entity artifacts.
/// </summary>
/// <typeparam name="T">The table entity type.</typeparam>
public class TableStorageEntityArtifactKind<T> : ArtifactKind<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>, IStaticArtifactKind<TableStorageEntityArtifactKind<T>>
    where T : class, ITableEntity
{
    /// <summary>
    /// Singleton artifact kind instance.
    /// </summary>
    public static TableStorageEntityArtifactKind<T> Kind { get; } = new TableStorageEntityArtifactKind<T>();
}
