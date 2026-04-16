using Azure.Data.Tables;
using System.Collections.Generic;
using TestFramework.Azure.DB.CosmosDB;
using TestFramework.Azure.DB.SqlServer;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.StorageAccount.Blob;
using TestFramework.Azure.StorageAccount.Table;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Extensions;

public static class AzureArtifactExtension
{
    public static ArtifactInstance<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactReference<TItem>> GetCosmosItemArtifact<TItem>(this ArtifactStore store, ArtifactIdentifier identifier)
    {
        return store.GetArtifact(CosmosDbItemArtifactKind<TItem>.Kind, identifier);
    }

    public static ITimelineRunBuilder AddCosmosItemArtifact<TItem>(this ITimelineRunBuilder run, ArtifactIdentifier identifier, CosmosContainerIdentifier dbIdentifier, TItem item)
    {
        return run.AddArtifact(identifier, new CosmosDbItemArtifactReference<TItem>(dbIdentifier), new CosmosDbItemArtifactData<TItem>(item));
    }

    public static ArtifactInstance<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>> GetTableEntityArtifact<T>(this ArtifactStore store, ArtifactIdentifier identifier) where T : class, ITableEntity
    {
        return store.GetArtifact(TableStorageEntityArtifactKind<T>.Kind, identifier);
    }

    public static ITimelineRunBuilder AddTableEntityArtifact<T>(this ITimelineRunBuilder run, ArtifactIdentifier artifactId, StorageAccountIdentifier storageId, string tableName, T entity) where T : class, ITableEntity
    {
        return run.AddArtifact(artifactId, new TableStorageEntityArtifactReference<T>(storageId, tableName, entity.PartitionKey, entity.RowKey), new TableStorageEntityArtifactData<T>(entity));
    }

    public static ArtifactInstance<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference> GetBlobArtifact(this ArtifactStore store, ArtifactIdentifier identifier)
    {
        return store.GetArtifact(StorageAccountBlobArtifactKind.Kind, identifier);
    }

    public static ITimelineRunBuilder AddBlobArtifact(this ITimelineRunBuilder run, ArtifactIdentifier artifactId, StorageAccountIdentifier storageId, string path, byte[] data, Dictionary<string, string>? metadata = null)
    {
        return run.AddArtifact(artifactId, new StorageAccountBlobArtifactReference(storageId, path), new StorageAccountBlobArtifactData(data, metadata ?? []));
    }

    // ── Typed TimelineRun assertion handles ──────────────────────────────────

    public static ArtifactHandle<StorageAccountBlobArtifactData> BlobArtifact(this TimelineRun run, ArtifactIdentifier identifier)
        => run.Artifact<StorageAccountBlobArtifactData>(identifier);

    public static ArtifactHandle<CosmosDbItemArtifactData<TItem>> CosmosArtifact<TItem>(this TimelineRun run, ArtifactIdentifier identifier)
        => run.Artifact<CosmosDbItemArtifactData<TItem>>(identifier);

    public static ArtifactHandle<TableStorageEntityArtifactData<T>> TableArtifact<T>(this TimelineRun run, ArtifactIdentifier identifier) where T : class, ITableEntity
        => run.Artifact<TableStorageEntityArtifactData<T>>(identifier);

    // ── SQL ──────────────────────────────────────────────────────────────────

    public static ArtifactInstance<SqlRowArtifactDescriber<T>, SqlRowArtifactData<T>, SqlRowArtifactReference<T>> GetSqlArtifact<T>(this ArtifactStore store, ArtifactIdentifier identifier) where T : class
        => store.GetArtifact(SqlRowArtifactKind<T>.Kind, identifier);

    public static ITimelineRunBuilder AddSqlArtifact<T>(this ITimelineRunBuilder run, ArtifactIdentifier artifactId, SqlDatabaseIdentifier dbIdentifier, T row, params VariableReference<string>[] primaryKeyValues) where T : class
        => run.AddArtifact(artifactId, new SqlRowArtifactReference<T>(dbIdentifier, primaryKeyValues), new SqlRowArtifactData<T>(row));

    public static ArtifactHandle<SqlRowArtifactData<T>> SqlArtifact<T>(this TimelineRun run, ArtifactIdentifier identifier) where T : class
        => run.Artifact<SqlRowArtifactData<T>>(identifier);
}