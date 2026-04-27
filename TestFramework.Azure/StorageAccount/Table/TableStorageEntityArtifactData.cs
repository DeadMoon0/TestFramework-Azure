using Azure.Data.Tables;
using System.Text.Json;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.StorageAccount.Table;

/// <summary>
/// Artifact payload that stores an Azure Table entity snapshot.
/// </summary>
/// <typeparam name="T">The table entity type.</typeparam>
public class TableStorageEntityArtifactData<T>(T entity) : ArtifactData<TableStorageEntityArtifactData<T>, TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactReference<T>>
    where T : class, ITableEntity
{
    /// <summary>
    /// The captured table entity value.
    /// </summary>
    public T Entity { get; } = entity;

    /// <summary>
    /// Returns a readable string representation of the table entity artifact.
    /// </summary>
    /// <returns>A string representation of the artifact data.</returns>
    public override string ToString()
    {
        try { return $"Table Entity<{typeof(T).Name}>: {Entity.PartitionKey}/{Entity.RowKey} {JsonSerializer.Serialize(Entity)}"; }
        catch { return $"Table Entity<{typeof(T).Name}>: {Entity.PartitionKey}/{Entity.RowKey}"; }
    }
}
