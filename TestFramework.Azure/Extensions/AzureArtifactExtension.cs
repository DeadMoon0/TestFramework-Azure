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

/// <summary>
/// Convenience extensions for creating, retrieving, and asserting Azure artifacts in timelines.
/// </summary>
public static class AzureArtifactExtension
{
    /// <summary>
    /// Retrieves a typed Cosmos artifact instance from the artifact store.
    /// </summary>
    /// <typeparam name="TItem">The Cosmos item type.</typeparam>
    /// <param name="store">The artifact store to read from.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <returns>The typed Cosmos artifact instance.</returns>
    public static ArtifactInstance<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactReference<TItem>> GetCosmosItemArtifact<TItem>(this ArtifactStore store, ArtifactIdentifier identifier)
    {
        return store.GetArtifact(CosmosDbItemArtifactKind<TItem>.Kind, identifier);
    }

    /// <summary>
    /// Registers a Cosmos artifact for timeline setup and cleanup.
    /// </summary>
    /// <typeparam name="TItem">The Cosmos item type.</typeparam>
    /// <param name="run">The timeline run builder.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <param name="dbIdentifier">The Cosmos container identifier.</param>
    /// <param name="item">The item to upsert during setup.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public static ITimelineRunBuilder AddCosmosItemArtifact<TItem>(this ITimelineRunBuilder run, ArtifactIdentifier identifier, CosmosContainerIdentifier dbIdentifier, TItem item)
    {
        return run.AddArtifact(identifier, new CosmosDbItemArtifactReference<TItem>(dbIdentifier), new CosmosDbItemArtifactData<TItem>(item));
    }

    /// <summary>
    /// Retrieves a typed Azure Table artifact instance from the artifact store.
    /// </summary>
    /// <typeparam name="T">The table entity type.</typeparam>
    /// <param name="store">The artifact store to read from.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <returns>The typed table artifact instance.</returns>
    public static ArtifactInstance<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>> GetTableEntityArtifact<T>(this ArtifactStore store, ArtifactIdentifier identifier) where T : class, ITableEntity
    {
        return store.GetArtifact(TableStorageEntityArtifactKind<T>.Kind, identifier);
    }

    /// <summary>
    /// Registers an Azure Table artifact for timeline setup and cleanup.
    /// </summary>
    /// <typeparam name="T">The table entity type.</typeparam>
    /// <param name="run">The timeline run builder.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="storageId">The Storage Account identifier.</param>
    /// <param name="tableName">The table name that contains the entity.</param>
    /// <param name="entity">The entity to upsert during setup.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public static ITimelineRunBuilder AddTableEntityArtifact<T>(this ITimelineRunBuilder run, ArtifactIdentifier artifactId, StorageAccountIdentifier storageId, string tableName, T entity) where T : class, ITableEntity
    {
        return run.AddArtifact(artifactId, new TableStorageEntityArtifactReference<T>(storageId, tableName, entity.PartitionKey, entity.RowKey), new TableStorageEntityArtifactData<T>(entity));
    }

    /// <summary>
    /// Retrieves a blob artifact instance from the artifact store.
    /// </summary>
    /// <param name="store">The artifact store to read from.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <returns>The typed blob artifact instance.</returns>
    public static ArtifactInstance<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference> GetBlobArtifact(this ArtifactStore store, ArtifactIdentifier identifier)
    {
        return store.GetArtifact(StorageAccountBlobArtifactKind.Kind, identifier);
    }

    /// <summary>
    /// Registers a blob artifact for timeline setup and cleanup.
    /// </summary>
    /// <param name="run">The timeline run builder.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="storageId">The Storage Account identifier.</param>
    /// <param name="path">The blob path inside the configured container.</param>
    /// <param name="data">The blob payload.</param>
    /// <param name="metadata">Optional blob metadata.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public static ITimelineRunBuilder AddBlobArtifact(this ITimelineRunBuilder run, ArtifactIdentifier artifactId, StorageAccountIdentifier storageId, string path, byte[] data, Dictionary<string, string>? metadata = null)
    {
        return run.AddArtifact(artifactId, new StorageAccountBlobArtifactReference(storageId, path), new StorageAccountBlobArtifactData(data, metadata ?? []));
    }

    // ── Typed TimelineRun assertion handles ──────────────────────────────────

    /// <summary>
    /// Returns an assertion handle for a blob artifact in a completed run.
    /// </summary>
    /// <param name="run">The completed timeline run.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <returns>An assertion handle for the blob artifact.</returns>
    public static ArtifactHandle<StorageAccountBlobArtifactData> BlobArtifact(this TimelineRun run, ArtifactIdentifier identifier)
        => run.Artifact<StorageAccountBlobArtifactData>(identifier);

    /// <summary>
    /// Returns an assertion handle for a Cosmos artifact in a completed run.
    /// </summary>
    /// <typeparam name="TItem">The Cosmos item type.</typeparam>
    /// <param name="run">The completed timeline run.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <returns>An assertion handle for the Cosmos artifact.</returns>
    public static ArtifactHandle<CosmosDbItemArtifactData<TItem>> CosmosArtifact<TItem>(this TimelineRun run, ArtifactIdentifier identifier)
        => run.Artifact<CosmosDbItemArtifactData<TItem>>(identifier);

    /// <summary>
    /// Returns an assertion handle for a table artifact in a completed run.
    /// </summary>
    /// <typeparam name="T">The table entity type.</typeparam>
    /// <param name="run">The completed timeline run.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <returns>An assertion handle for the table artifact.</returns>
    public static ArtifactHandle<TableStorageEntityArtifactData<T>> TableArtifact<T>(this TimelineRun run, ArtifactIdentifier identifier) where T : class, ITableEntity
        => run.Artifact<TableStorageEntityArtifactData<T>>(identifier);

    // ── SQL ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves a typed SQL artifact instance from the artifact store.
    /// </summary>
    /// <typeparam name="T">The row entity type.</typeparam>
    /// <param name="store">The artifact store to read from.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <returns>The typed SQL artifact instance.</returns>
    public static ArtifactInstance<SqlRowArtifactDescriber<T>, SqlRowArtifactData<T>, SqlRowArtifactReference<T>> GetSqlArtifact<T>(this ArtifactStore store, ArtifactIdentifier identifier) where T : class
        => store.GetArtifact(SqlRowArtifactKind<T>.Kind, identifier);

    /// <summary>
    /// Registers a SQL artifact for timeline setup and cleanup.
    /// </summary>
    /// <typeparam name="T">The row entity type.</typeparam>
    /// <param name="run">The timeline run builder.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="dbIdentifier">The SQL database identifier.</param>
    /// <param name="row">The row to insert or update during setup.</param>
    /// <param name="primaryKeyValues">Primary key values used to resolve the row later.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public static ITimelineRunBuilder AddSqlArtifact<T>(this ITimelineRunBuilder run, ArtifactIdentifier artifactId, SqlDatabaseIdentifier dbIdentifier, T row, params VariableReference<string>[] primaryKeyValues) where T : class
        => run.AddArtifact(artifactId, new SqlRowArtifactReference<T>(dbIdentifier, primaryKeyValues), new SqlRowArtifactData<T>(row));

    /// <summary>
    /// Returns an assertion handle for a SQL artifact in a completed run.
    /// </summary>
    /// <typeparam name="T">The row entity type.</typeparam>
    /// <param name="run">The completed timeline run.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <returns>An assertion handle for the SQL artifact.</returns>
    public static ArtifactHandle<SqlRowArtifactData<T>> SqlArtifact<T>(this TimelineRun run, ArtifactIdentifier identifier) where T : class
        => run.Artifact<SqlRowArtifactData<T>>(identifier);
}