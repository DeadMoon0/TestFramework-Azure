using Azure.Data.Tables;
using System.Text.Json;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.StorageAccount.Table;

public class TableStorageEntityArtifactData<T>(T entity) : ArtifactData<TableStorageEntityArtifactData<T>, TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactReference<T>>
    where T : class, ITableEntity
{
    public T Entity { get; } = entity;

    public override string ToString()
    {
        try { return $"Table Entity<{typeof(T).Name}>: {Entity.PartitionKey}/{Entity.RowKey} {JsonSerializer.Serialize(Entity)}"; }
        catch { return $"Table Entity<{typeof(T).Name}>: {Entity.PartitionKey}/{Entity.RowKey}"; }
    }
}
