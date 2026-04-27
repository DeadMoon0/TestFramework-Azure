using System;

namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// Configuration required to reach Azure Storage resources used by blobs, tables, and optional queues.
/// </summary>
/// <remarks>
/// The identifier maps to a named entry under the <c>StorageAccount</c> section.
/// <see cref="ConnectionString"/> is always required. Container/table names are required only for the Azure features that use them.
/// </remarks>
public record StorageAccountConfig
{
    /// <summary>
    /// Storage account connection string.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Optional queue name when queue-backed storage operations need a default queue.
    /// </summary>
    public required string? QueueContainerName { get; init; }

    /// <summary>
    /// Optional blob container name used by blob artifact helpers and blob liveness checks.
    /// </summary>
    public required string? BlobContainerName { get; init; }

    /// <summary>
    /// Optional table name used by table artifact helpers and table liveness checks.
    /// </summary>
    public required string? TableContainerName { get; init; }

    /// <summary>
    /// Gets the configured queue name or throws when no queue name has been supplied.
    /// </summary>
    public string QueueContainerNameRequired => QueueContainerName ?? throw new InvalidOperationException("QueueContainerName is required.");

    /// <summary>
    /// Gets the configured blob container name or throws when no blob container name has been supplied.
    /// </summary>
    public string BlobContainerNameRequired => BlobContainerName ?? throw new InvalidOperationException("BlobContainerName is required.");

    /// <summary>
    /// Gets the configured table name or throws when no table name has been supplied.
    /// </summary>
    public string TableContainerNameRequired => TableContainerName ?? throw new InvalidOperationException("TableContainerName is required.");
}