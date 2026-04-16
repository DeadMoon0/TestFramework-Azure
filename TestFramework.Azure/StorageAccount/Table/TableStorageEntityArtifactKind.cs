using Azure.Data.Tables;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.StorageAccount.Table;

public class TableStorageEntityArtifactKind<T> : ArtifactKind<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>, IStaticArtifactKind<TableStorageEntityArtifactKind<T>>
    where T : class, ITableEntity
{
    public static TableStorageEntityArtifactKind<T> Kind { get; } = new TableStorageEntityArtifactKind<T>();
}
