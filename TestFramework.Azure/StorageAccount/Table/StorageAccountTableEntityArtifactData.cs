using Newtonsoft.Json;
using Azure.Data.Tables;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.StorageAccount.Table;

/// <summary>
/// Artifact payload that stores an Azure Table entity snapshot.
/// </summary>
/// <typeparam name="T">The table entity type.</typeparam>
public class StorageAccountTableEntityArtifactData<T>(T entity) : ArtifactData<StorageAccountTableEntityArtifactData<T>, StorageAccountTableEntityArtifactDescriber<T>, StorageAccountTableEntityArtifactReference<T>>
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
        try { return $"Table Entity<{typeof(T).Name}>: {Entity.PartitionKey}/{Entity.RowKey} {JsonConvert.SerializeObject(Entity)}"; }
        catch { return $"Table Entity<{typeof(T).Name}>: {Entity.PartitionKey}/{Entity.RowKey}"; }
    }
}
